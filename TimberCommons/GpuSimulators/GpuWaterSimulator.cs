// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Timberborn.AssetSystem;
using Timberborn.MapIndexSystem;
using Timberborn.WaterSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityDev.Utils.ShaderPipeline;
using UnityEngine;
using Object = UnityEngine.Object;
using IgorZ.TimberCommons.WaterService;

namespace IgorZ.TimberCommons.GpuSimulators {

sealed class GpuWaterSimulator {

  #region API

  /// <summary>Sets up shader related stuff. Must be called when teh stock simulator is already set.</summary>
  public void Initialize(GpuSimulatorsController gpuSimulatorsController) {
    SetupShader(gpuSimulatorsController);
  }

  /// <summary>Executes the logic and updates the stock simulators with the processed data.</summary>
  public void TickPipeline() {
    _stopwatch.Restart();

    PreparePipeline();
    _shaderPipeline.RunBlocking();
    //ProcessOutput();

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
  readonly DirectWaterServiceAccessor _directWaterServiceAccessor;

  // Inputs.
  // ReSharper disable NotAccessedField.Local
  struct InputStruct2 {
    public int ImpermeableSurfaceServiceHeight;
    public int ImpermeableSurfaceServiceMinFlowSlower;
    public float EvaporationModifier;
    public uint BitmapFlags;

    public const uint PartialObstaclesBit = 0x0001;
    public const uint IsInActualMapBit = 0x0002;
  };
  // ReSharper restore NotAccessedField.Local

  InputStruct2[] _packedInput2;

  ShaderPipeline _shaderPipeline;
  int _totalMapSize;

  GpuWaterSimulator(WaterSimulator simulator, IWaterSimulationController waterSimulationController,
                    IResourceAssetLoader resourceAssetLoader, MapIndexService mapIndexService,
                    DirectWaterServiceAccessor directWaterServiceAccessor) {
    _simulator = simulator;
    _waterSimulationController = waterSimulationController as WaterSimulationController;
    _resourceAssetLoader = resourceAssetLoader;
    _mapIndexService = mapIndexService;
    _directWaterServiceAccessor = directWaterServiceAccessor;
  }

  void SetupShader(GpuSimulatorsController gpuSimulatorsController) {
    // _totalMapSize = _mapIndexService.TotalMapSize;
    // var mapDataSize = new Vector3Int(_mapIndexService.MapSize.x, _mapIndexService.MapSize.y, 1);
    // var waterSimulationSettings = _simulator._waterSimulationSettings;
    // var waterContaminationSimulationSettings = _simulator._waterContaminationSimulationSettings;
    //
    // _packedInput2 = new InputStruct2[_totalMapSize];
    //
    // var prefab = _resourceAssetLoader.Load<ComputeShader>(SimulatorShaderName);
    // var shader = Object.Instantiate(prefab);
    //
    // _shaderPipeline = ShaderPipeline.NewBuilder(shader)
    //     // Simulation settings.
    //     // Common.
    //     .WithConstantValue("Stride", _mapIndexService.Stride)
    //     .WithConstantValue("DeltaTime", Time.fixedDeltaTime * waterSimulationSettings.TimeScale)
    //     .WithConstantValue("MaxContamination", WaterSimulator.MaxContamination)
    //     // WaterSimulationSettings
    //     .WithConstantValue("FastEvaporationDepthThreshold", waterSimulationSettings.FastEvaporationDepthThreshold)
    //     .WithConstantValue("FastEvaporationSpeed", waterSimulationSettings.FastEvaporationSpeed)
    //     .WithConstantValue("FlowSlowerOutflowMaxInflowPart", waterSimulationSettings.FlowSlowerOutflowMaxInflowPart)
    //     .WithConstantValue("FlowSlowerOutflowPenalty", waterSimulationSettings.FlowSlowerOutflowPenalty)
    //     .WithConstantValue("FlowSlowerOutflowPenaltyThreshold", waterSimulationSettings.FlowSlowerOutflowPenaltyThreshold)
    //     .WithConstantValue("HardDamThreshold", waterSimulationSettings.HardDamThreshold)
    //     .WithConstantValue("MaxHardDamDecrease", waterSimulationSettings.MaxHardDamDecrease)
    //     .WithConstantValue("MaxWaterfallOutflow", waterSimulationSettings.MaxWaterfallOutflow)
    //     .WithConstantValue("MidDamThreshold", waterSimulationSettings.MidDamThreshold)
    //     .WithConstantValue("NormalEvaporationSpeed", waterSimulationSettings.NormalEvaporationSpeed)
    //     .WithConstantValue("OutflowBalancingScaler", waterSimulationSettings.OutflowBalancingScaler)
    //     .WithConstantValue("SoftDamThreshold", waterSimulationSettings.SoftDamThreshold)
    //     .WithConstantValue("WaterFlowSpeed", waterSimulationSettings.WaterFlowSpeed)
    //     .WithConstantValue("WaterSpillThreshold", waterSimulationSettings.WaterSpillThreshold)
    //     // WaterContaminationSimulationSettings
    //     .WithConstantValue("DiffusionDepthLimit", waterContaminationSimulationSettings.DiffusionDepthLimit)
    //     .WithConstantValue("DiffusionOutflowLimit", waterContaminationSimulationSettings.DiffusionOutflowLimit)
    //     .WithConstantValue("DiffusionRate", waterContaminationSimulationSettings.DiffusionRate)
    //     // All buffers.
    //     .WithInputBuffer("PackedInput2", _packedInput2)
    //     .WithIntermediateBuffer("TempOutflowsBuff", sizeof(float) * 4, _totalMapSize)
    //     .WithIntermediateBuffer("InitialWaterDepthsBuff", sizeof(float), _totalMapSize)
    //     .WithIntermediateBuffer("ContaminationsBufferBuff", sizeof(float), _totalMapSize)
    //     .WithIntermediateBuffer("ContaminationDiffusionsBuff", sizeof(float) + 4*4, _totalMapSize)
    //     .WithOutputBuffer("OutflowsBuff", _simulator._waterMap.Outflows)  // Must be pushed io enable 
    //     //.WithOutputBuffer("WaterDepthsBuff", _simulator._waterMap.WaterDepths)
    //     //.WithOutputBuffer("ContaminationsBuff", _simulator._waterContaminationMap.Contaminations)
    //     .WithIntermediateBuffer(gpuSimulatorsController.WaterDepthsBuffer)
    //     .WithIntermediateBuffer(gpuSimulatorsController.ContaminationsBuffer)
    //     // The kernel chain! They will execute in the order they are declared.
    //     .DispatchKernel(
    //         "SavePreviousState", new Vector3Int(_totalMapSize, 1, 1),
    //         "i:WaterDepthsBuff", "i:ContaminationsBuff",
    //         "o:InitialWaterDepthsBuff", "o:ContaminationsBufferBuff")
    //     .DispatchKernel(
    //         "UpdateOutflows", mapDataSize,
    //         "s:PackedInput2", "i:WaterDepthsBuff", "s:OutflowsBuff",
    //         "o:TempOutflowsBuff")
    //     .DispatchKernel(
    //         "UpdateWaterParameters", mapDataSize,
    //         "s:PackedInput2", "i:WaterDepthsBuff", "i:ContaminationsBuff", "s:OutflowsBuff",
    //         "i:TempOutflowsBuff", "i:InitialWaterDepthsBuff",
    //         "o:ContaminationsBufferBuff",
    //         "r:OutflowsBuff", "o:WaterDepthsBuff")
    //     .DispatchKernel(
    //         "SimulateContaminationDiffusion1", mapDataSize,
    //         "s:PackedInput2", "i:WaterDepthsBuff",
    //         "i:TempOutflowsBuff",
    //         "o:ContaminationDiffusionsBuff")
    //     .DispatchKernel(
    //         "SimulateContaminationDiffusion2", mapDataSize,
    //         "i:WaterDepthsBuff",
    //         "i:ContaminationsBufferBuff", "i:ContaminationDiffusionsBuff",
    //         "o:ContaminationsBuff")
    //     .Build();
    //
    // _waterDepthsBuffer = gpuSimulatorsController.WaterDepthsBuffer;
    // _contaminationsBuffer = gpuSimulatorsController.ContaminationsBuffer;
    //
    // DebugEx.Warning("*** Shader execution plan:\n{0}", string.Join("\n", _shaderPipeline.GetExecutionPlan()));
  }

  BaseBuffer _waterDepthsBuffer;
  BaseBuffer _contaminationsBuffer;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void PreparePipeline() {
    var sim = _simulator;

    // Constant starting part from the original sim.
    sim._deltaTime = sim._fixedDeltaTime * sim._waterSimulationSettings.TimeScale;
    sim.UpdateWaterSources();
    sim.UpdateWaterChanges();
    
    // FIXME: WHY BUMP?!
    //_directWaterServiceAccessor.UpdateDepthsCallback(_simulator._deltaTime);
    
    //FIXME: here push water/contamination.
    _waterDepthsBuffer.PushToGpu(null);
    _contaminationsBuffer.PushToGpu(null);

    // Load data.
    // FIXME: This data is not updated every frame. Make it event driven.
    for (var index = 0; index < _totalMapSize; index++) {
      uint bitmapFlags = 0;
      if (sim._impermeableSurfaceService.PartialObstacles[index]) {
        bitmapFlags |= InputStruct2.PartialObstaclesBit;
      }
      if (sim._mapIndexService.IndexIsInActualMap(index)) {
        // FIXME: This is a constant array that is filled once on game load.
        bitmapFlags |= InputStruct2.IsInActualMapBit;
      }
      _packedInput2[index] = new InputStruct2 {
          ImpermeableSurfaceServiceHeight = sim._impermeableSurfaceService.Heights[index],
          ImpermeableSurfaceServiceMinFlowSlower = sim._impermeableSurfaceService.MinFlowSlowers[index],
          // FIXME: It's us who fills it (moisture sims). It's output only.
          EvaporationModifier = sim._threadSafeWaterEvaporationMap.EvaporationModifiers[index],
          BitmapFlags = bitmapFlags,
      };
    }
  }

  public void ProcessOutput() {
    _directWaterServiceAccessor.UpdateDepthsCallback(_simulator._deltaTime);
    _simulator._transientWaterMap.Refresh();
  }

  public void EnableSimulator() {
    DebugEx.Warning("*** Enabling water GPU sim-1");
    DebugEx.Warning("*** sim speed: {0}", _waterSimulationController._simulationSpeed);
  }

  public void DisableSimulator() {
    DebugEx.Warning("*** Disabling water GPU sim-1");
  }

  #endregion
}

}
