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

sealed class GpuWaterSimulator : IGpuSimulatorStats {

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
    var stopwatch = Stopwatch.StartNew();

    // Constant staring part from the original sim.
    _simulator._deltaTime = _simulator._fixedDeltaTime * _simulator._waterSimulationSettings.TimeScale;
    _simulator.UpdateWaterSources();
    _simulator.UpdateWaterChanges();

    PrepareInputData();
    _shaderPipeline.RunBlocking();
    FlushOutputData();

    // Constant ending part from the original sim.
    _simulator._transientWaterMap.Refresh();

    stopwatch.Stop();
    _totalSimPerfSampler.AddSample(stopwatch.Elapsed.TotalSeconds);
    stopwatch.Reset();
    _shaderPerfSampler.AddSample(_shaderPipeline.LastRunDuration.TotalSeconds);
  }

  #endregion

  #region Implementation

  const string SimulatorShaderName = "igorz.timbercommons/shaders/WaterSimulatorPacked";

  readonly WaterSimulator _simulator;
  readonly IResourceAssetLoader _resourceAssetLoader;
  readonly MapIndexService _mapIndexService;

  readonly ValueSampler _shaderPerfSampler = new(10);
  readonly ValueSampler _totalSimPerfSampler = new(10);

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

  GpuWaterSimulator(WaterSimulator simulator, IResourceAssetLoader resourceAssetLoader,
                    MapIndexService mapIndexService) {
    _simulator = simulator;
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
        .WithConstantValue("DeltaTime", Time.fixedDeltaTime * waterSimulationSettings.TimeScale)//FIXME: every frame?
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
        //FIXME: use actual types? or fuck it?
        .WithIntermediateBuffer("TempOutflowsBuff", sizeof(float) * 4, _totalMapSize)
        .WithIntermediateBuffer("InitialWaterDepthsBuff", sizeof(float), _totalMapSize)
        .WithIntermediateBuffer("ContaminationsBufferBuff", sizeof(float), _totalMapSize)
        .WithIntermediateBuffer("ContaminationDiffusionsBuff", sizeof(float) + 4*4, _totalMapSize)
        .WithOutputBuffer("OutflowsBuff", _simulator._waterMap.Outflows)
        .WithOutputBuffer("WaterDepthsBuff", _simulator._waterMap.WaterDepths)
        .WithOutputBuffer("ContaminationsBuff", _simulator._waterContaminationMap.Contaminations)
        // The kernel chain! They will execute in the order they are declared.
        .DispatchKernel(
            "SavePreviousState997", new Vector3Int(_totalMapSize, 1, 1),
            "s:WaterDepthsBuff", "s:ContaminationsBuff",
            "o:InitialWaterDepthsBuff", "o:ContaminationsBufferBuff")
        .DispatchKernel(
            "UpdateOutflows", mapDataSize,
            "s:PackedInput1", "s:OutflowsBuff", "s:ContaminationsBuff", "s:WaterDepthsBuff",
            "o:TempOutflowsBuff")
        .DispatchKernel(
            "UpdateWaterParameters", mapDataSize,
            "s:PackedInput1", "s:WaterDepthsBuff", "s:ContaminationsBuff",
            "i:TempOutflowsBuff", "i:InitialWaterDepthsBuff", "i:ContaminationsBufferBuff",
            "r:OutflowsBuff", "r:WaterDepthsBuff")
        .DispatchKernel(
            "SimulateContaminationDiffusion1", mapDataSize,
            "s:PackedInput1",
            "i:WaterDepthsBuff", "i:TempOutflowsBuff",
            "o:ContaminationDiffusionsBuff")
        .DispatchKernel(
            "SimulateContaminationDiffusion2", mapDataSize,
            "i:WaterDepthsBuff", "i:ContaminationsBufferBuff", "i:ContaminationDiffusionsBuff",
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
  }

  void DisableSimulator() {
    DebugEx.Warning("*** Disabling water GPU sim-1");
  }

  #endregion

  #region GpuSimulatorStats

  /// <inheritdoc/>
  public (double min, double max, double avg, double mean) GetShaderStats() {
    return _shaderPerfSampler.GetStats();
  }

  /// <inheritdoc/>
  public (double min, double max, double avg, double mean) GetTotalStats() {
    return _totalSimPerfSampler.GetStats();
  }

  #endregion
}

}
