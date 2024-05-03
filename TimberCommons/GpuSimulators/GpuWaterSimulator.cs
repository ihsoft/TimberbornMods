// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Timberborn.AssetSystem;
using Timberborn.MapIndexSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.WaterSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityDev.Utils.ShaderPipeline;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgorZ.TimberCommons.GpuSimulators {

sealed class GpuWaterSimulator {

  #region API

  /// <summary>Tells whether that simulator is running.</summary>
  public bool IsEnabled {
    get => _isEnabled;
    set {
      if (value == _isEnabled) {
        return;
      }
      _isEnabled = value;
      if (value) {
        EnableSimulator();
      } else {
        DisableSimulator();
      }
    }
  }
  bool _isEnabled;

  /// <summary>Sets up shader related stuff. Must be called when teh stock simulator is already set.</summary>
  public void Initialize() {
    SetupShader();
  }

  /// <summary>Executes the logic and updates the stock simulators with the processed data.</summary>
  public void TickPipeline() {
    _stopwatch.Restart();

    // Constant staring part from the original sim.
    _simulator._deltaTime = _simulator._fixedDeltaTime * _simulator._waterSimulationSettings.TimeScale;
    _simulator.UpdateWaterSources();
    _simulator.UpdateWaterChanges();

    PrepareInputData();
    _shaderPipeline.RunBlocking();
    FlushOutputData();

    // Constant ending part from the original sim.
    // FIXME: Can be in shader too.
    _simulator._transientWaterMap.Refresh();

    _stopwatch.Stop();
    TotalSimPerfSampler.AddSample(_stopwatch.Elapsed.TotalSeconds);
    ShaderPerfSampler.AddSample(_shaderPipeline.LastRunDuration.TotalSeconds);
  }
  readonly Stopwatch _stopwatch = new();

  internal readonly ValueSampler ShaderPerfSampler = new(10);
  internal readonly ValueSampler TotalSimPerfSampler = new(10);

  #endregion

  #region Implementation

  const string SimulatorShaderName = "igorz.timbercommons/shaders/WaterSimulatorPacked";

  readonly WaterSimulator _simulator;
  readonly WaterSimulationController _waterSimulationController;
  readonly IResourceAssetLoader _resourceAssetLoader;
  readonly MapIndexService _mapIndexService;

  // Inputs.
  // ReSharper disable NotAccessedField.Local
  struct InputStruct1 {
    public int ImpermeableSurfaceServiceHeight;
    public int ImpermeableSurfaceServiceMinFlowSlower;
    public float EvaporationModifier;
    public uint BitmapFlags;

    public const uint PartialObstaclesBit = 0x0001;
    public const uint IsInActualMapBit = 0x0002;
  };
  // ReSharper restore NotAccessedField.Local

  InputStruct1[] _packedInput1;

  ShaderPipeline _shaderPipeline;
  int _totalMapSize;

  GpuWaterSimulator(WaterSimulator simulator, IWaterSimulationController waterSimulationController,
                    IResourceAssetLoader resourceAssetLoader, MapIndexService mapIndexService) {
    _simulator = simulator;
    _waterSimulationController = waterSimulationController as WaterSimulationController;
    _resourceAssetLoader = resourceAssetLoader;
    _mapIndexService = mapIndexService;
  }

  void SetupShader() {
    _totalMapSize = _mapIndexService.TotalMapSize;
    var mapDataSize = new Vector3Int(_mapIndexService.MapSize.x, _mapIndexService.MapSize.y, 1);
    var waterSimulationSettings = _simulator._waterSimulationSettings;
    var waterContaminationSimulationSettings = _simulator._waterContaminationSimulationSettings;

    _packedInput1 = new InputStruct1[_totalMapSize];

    var prefab = _resourceAssetLoader.Load<ComputeShader>(SimulatorShaderName);
    var shader = Object.Instantiate(prefab);

    _shaderPipeline = ShaderPipeline.NewBuilder(shader)
        // Simulation settings.
        // Common.
        .WithConstantValue("Stride", _mapIndexService.Stride)
        .WithConstantValue("DeltaTime", Time.fixedDeltaTime * waterSimulationSettings.TimeScale)
        .WithConstantValue("MaxContamination", WaterSimulator.MaxContamination)
        // WaterSimulationSettings
        .WithConstantValue("FastEvaporationDepthThreshold", waterSimulationSettings.FastEvaporationDepthThreshold)
        .WithConstantValue("FastEvaporationSpeed", waterSimulationSettings.FastEvaporationSpeed)
        .WithConstantValue("FlowSlowerOutflowMaxInflowPart", waterSimulationSettings.FlowSlowerOutflowMaxInflowPart)
        .WithConstantValue("FlowSlowerOutflowPenalty", waterSimulationSettings.FlowSlowerOutflowPenalty)
        .WithConstantValue("FlowSlowerOutflowPenaltyThreshold", waterSimulationSettings.FlowSlowerOutflowPenaltyThreshold)
        .WithConstantValue("HardDamThreshold", waterSimulationSettings.HardDamThreshold)
        .WithConstantValue("MaxHardDamDecrease", waterSimulationSettings.MaxHardDamDecrease)
        .WithConstantValue("MaxWaterfallOutflow", waterSimulationSettings.MaxWaterfallOutflow)
        .WithConstantValue("MidDamThreshold", waterSimulationSettings.MidDamThreshold)
        .WithConstantValue("NormalEvaporationSpeed", waterSimulationSettings.NormalEvaporationSpeed)
        .WithConstantValue("OutflowBalancingScaler", waterSimulationSettings.OutflowBalancingScaler)
        .WithConstantValue("SoftDamThreshold", waterSimulationSettings.SoftDamThreshold)
        .WithConstantValue("WaterFlowSpeed", waterSimulationSettings.WaterFlowSpeed)
        .WithConstantValue("WaterSpillThreshold", waterSimulationSettings.WaterSpillThreshold)
        // WaterContaminationSimulationSettings
        .WithConstantValue("DiffusionDepthLimit", waterContaminationSimulationSettings.DiffusionDepthLimit)
        .WithConstantValue("DiffusionOutflowLimit", waterContaminationSimulationSettings.DiffusionOutflowLimit)
        .WithConstantValue("DiffusionRate", waterContaminationSimulationSettings.DiffusionRate)
        // All buffers.
        .WithInputBuffer("PackedInput1", _packedInput1)
        .WithIntermediateBuffer("TempOutflowsBuff", sizeof(float) * 4, _totalMapSize)
        .WithIntermediateBuffer("InitialWaterDepthsBuff", sizeof(float), _totalMapSize)
        .WithIntermediateBuffer("ContaminationsBufferBuff", sizeof(float), _totalMapSize)
        .WithIntermediateBuffer("ContaminationDiffusionsBuff", sizeof(float) + 4*4, _totalMapSize)
        .WithOutputBuffer("OutflowsBuff", _simulator._waterMap.Outflows)
        .WithOutputBuffer("WaterDepthsBuff", _simulator._waterMap.WaterDepths)
        .WithOutputBuffer("ContaminationsBuff", _simulator._waterContaminationMap.Contaminations)
        // The kernel chain! They will execute in the order they are declared.
        .DispatchKernel(
            "SavePreviousState991", new Vector3Int(_totalMapSize, 1, 1),
            "s:WaterDepthsBuff", "s:ContaminationsBuff",
            "o:InitialWaterDepthsBuff", "o:ContaminationsBufferBuff")
        .DispatchKernel(
            "UpdateOutflows", mapDataSize,
            "s:PackedInput1", "s:WaterDepthsBuff", "s:OutflowsBuff", "s:ContaminationsBuff",
            "o:TempOutflowsBuff")
        .DispatchKernel(
            "UpdateWaterParameters", mapDataSize,
            "s:PackedInput1", "s:WaterDepthsBuff", "s:ContaminationsBuff", "s:OutflowsBuff",
            "i:TempOutflowsBuff", "i:InitialWaterDepthsBuff",
            "o:ContaminationsBufferBuff",
            "r:OutflowsBuff", "r:WaterDepthsBuff")
        .DispatchKernel(
            "SimulateContaminationDiffusion1", mapDataSize,
            "s:PackedInput1", "s:WaterDepthsBuff",
            "i:TempOutflowsBuff",
            "o:ContaminationDiffusionsBuff")
        .DispatchKernel(
            "SimulateContaminationDiffusion2", mapDataSize,
            "s:WaterDepthsBuff",
            "i:ContaminationsBufferBuff", "i:ContaminationDiffusionsBuff",
            "r:ContaminationsBuff")
        .Build();
    DebugEx.Warning("*** Shader execution plan:\n{0}", string.Join("\n", _shaderPipeline.GetExecutionPlan()));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void PrepareInputData() {
    var sim = _simulator;
    for (var index = 0; index < _totalMapSize; index++) {
      var bitmapFlags =
          (sim._impermeableSurfaceService.PartialObstacles[index] ? InputStruct1.PartialObstaclesBit : 0)
          | (sim._mapIndexService.IndexIsInActualMap(index) ? InputStruct1.IsInActualMapBit : 0);
      _packedInput1[index] = new InputStruct1 {
          ImpermeableSurfaceServiceHeight = sim._impermeableSurfaceService.Heights[index],
          ImpermeableSurfaceServiceMinFlowSlower = sim._impermeableSurfaceService.MinFlowSlowers[index],
          EvaporationModifier = sim._threadSafeWaterEvaporationMap.EvaporationModifiers[index],
          BitmapFlags = bitmapFlags,
      };
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void FlushOutputData() {
  }

  void EnableSimulator() {
    DebugEx.Warning("*** Enabling water GPU sim-1");
    DebugEx.Warning("*** sim speed: {0}", _waterSimulationController._simulationSpeed);
  }

  void DisableSimulator() {
    DebugEx.Warning("*** Disabling water GPU sim-1");
  }

  #endregion
}

}
