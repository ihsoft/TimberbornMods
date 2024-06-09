// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics;
using IgorZ.TimberCommons.WaterService;
using Timberborn.AssetSystem;
using Timberborn.MapIndexSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoilBarrierSystem;
using Timberborn.SoilContaminationSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.TerrainSystem;
using Timberborn.WaterContaminationSystem;
using Timberborn.WaterSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityDev.Utils.ShaderPipeline;
using UnityEngine;
using UnityEngine.Rendering;

namespace IgorZ.TimberCommons.GpuSimulators {

/// <summary>This class substitutes the normal game simulation logic.</summary>
/// <remarks>
/// <p>
/// It disables all the tick simulations on the stock simulators and runs its own logic in the Unity physics update
/// methods. It's a big difference form what the stock game does! From the physics thread, this controller executes
/// shader based simulators. All the simulation logics is ran in parallel on GPU, and the result are fetched back once
/// they are ready.
/// </p>
/// <p>
/// This approach gives benefits on a good video card with the advanced support for compute shaders. However, if the
/// card is not powerful enough, it's better to stick to the stock or multi-thread approach.
/// </p>
/// </remarks>
sealed class GpuSimulatorsController : IPostLoadableSingleton {

  readonly MapIndexService _mapIndexService;
  readonly SoilBarrierMap _soilBarrierMap;
  readonly TerrainMap _terrainMap;
  readonly IResourceAssetLoader _resourceAssetLoader;
  readonly WaterSimulator _waterSimulator;
  readonly SoilMoistureSimulator _soilMoistureSimulator;
  readonly SoilContaminationSimulator _soilContaminationSimulator;
  readonly WaterMap _waterMap;
  readonly WaterContaminationMap _waterContaminationMap;
  readonly DirectWaterServiceAccessor _directWaterServiceAccessor;

  readonly WaterSimulationController _waterSimulationController;
  readonly SoilMoistureSimulationController _soilMoistureSimulationController;
  readonly SoilContaminationSimulationController _soilContaminationSimulationController;

  readonly Stopwatch _runShadersStopwatch = new();
  readonly ValueSampler _runShadersSampler = new(10);
  readonly Stopwatch _prepareInputsStopwatch = new();
  readonly ValueSampler _prepareInputsSampler = new(10);
  readonly Stopwatch _updateOutputsStopwatch = new();
  readonly ValueSampler _updateOutputsSampler = new(10);

  internal static GpuSimulatorsController Self;
  internal bool SimulatorEnabled { get; private set; }

  int _totalMapSize;
  Vector3Int _mapDataSize;
  int _mapIndexStride;

  #region Implemenatation

  GpuSimulatorsController(
      MapIndexService mapIndexService,
      SoilBarrierMap soilBarrierMap,
      TerrainMap terrainMap,
      IResourceAssetLoader resourceAssetLoader,
      WaterMap waterMap,
      WaterContaminationMap waterContaminationMap,
      DirectWaterServiceAccessor directWaterServiceAccessor,
      // Stock sims.
      WaterSimulator waterSimulator,
      SoilMoistureSimulator soilMoistureSimulator,
      SoilContaminationSimulator soilContaminationSimulator,
      // For debug panel only.
      IWaterSimulationController waterSimulationController,
      ISoilMoistureSimulationController soilMoistureSimulationController,
      ISoilContaminationSimulationController soilContaminationSimulationController) {
    _mapIndexService = mapIndexService;
    _soilBarrierMap = soilBarrierMap;
    _terrainMap = terrainMap;
    _resourceAssetLoader = resourceAssetLoader;
    _waterMap = waterMap;
    _waterContaminationMap = waterContaminationMap;
    _waterSimulator = waterSimulator;
    _directWaterServiceAccessor = directWaterServiceAccessor;
    _soilMoistureSimulator = soilMoistureSimulator;
    _soilContaminationSimulator = soilContaminationSimulator;
    _waterSimulationController = (WaterSimulationController) waterSimulationController;
    _soilMoistureSimulationController = (SoilMoistureSimulationController) soilMoistureSimulationController;
    _soilContaminationSimulationController =
        (SoilContaminationSimulationController) soilContaminationSimulationController;
    Self = this;
  }

  internal void EnableSimulator(bool state) {
    SimulatorEnabled = state;
    if (state) {
      _waterSimulationController.LastTickDurationMs = 0;
      _soilMoistureSimulationController.LastTickDurationMs = 0;
      _soilContaminationSimulationController.LastTickDurationMs = 0;
      OnSimulationEnabled();
    } else {
      OnSimulationDisabled();
    }
  }

  internal string GetStatsText() {
    if (!SimulatorEnabled) {
      return "GPU simulation disabled.";
    }
    var text = new List<string>(15);
    var totalCost = 0.0;
    {
      var (_, _, _, total) = _prepareInputsSampler.GetStats();
      totalCost += total;
      text.Add($"Prepare input cost: {total * 1000:0.##} ms");
    }
    {
      var (_, _, _, total) = _updateOutputsSampler.GetStats();
      totalCost += total;
      text.Add($"Handle output cost: {total * 1000:0.##} ms");
    }
    {
      var (_, _, _, total) = _runShadersSampler.GetStats();
      totalCost += total;
      text.Add($"Shaders run cost: {total * 1000:0.##} ms");
    }
    text.Add($"Cost per fixed frame: {totalCost * 1000:0.##} ms");
    return string.Join("\n", text);
  }

  #endregion

  #region Buffers

  // FIXME: Unwrap it and update each buffer on event.
  struct InputStruct1 {
    public float unused0; // Contaminations
    public float unused1; // WaterDepths
    public int unused2;   // UnsafeCellHeights;
    public uint BitmapFlags;

    public const uint ContaminationBarrierBit = 0x0001;
    public const uint AboveMoistureBarrierBit = 0x0002;
    public const uint FullMoistureBarrierBit = 0x0004;
    public const uint WaterTowerIrrigatedBit = 0x0008;
  };

  struct InputStruct2 {
    public int ImpermeableSurfaceServiceHeight;
    public int ImpermeableSurfaceServiceMinFlowSlower;
    public float EvaporationModifier;
    public uint BitmapFlags;

    public const uint PartialObstaclesBit = 0x0001;
    public const uint IsInActualMapBit = 0x0002;
  };

  SimpleBuffer<InputStruct1> _packedInput1Buffer;
  SimpleBuffer<InputStruct2> _packedInput2Buffer;

  SimpleBuffer<float> _moistureLevelsBuff;
  SimpleBuffer<float> _waterEvaporationModifierBuff;
  AppendBuffer<int> _moistureLevelsChangedLastTickBuffer;

  AppendBuffer<int> _contaminationsChangedLastTickBuffer;
  SimpleBuffer<float> _contaminationCandidatesBuffer;
  SimpleBuffer<float> _contaminationLevelsBuffer;

  SimpleBuffer<float> _waterDepthsBuffer;
  SimpleBuffer<float> _contaminationsBuffer;
  SimpleBuffer<WaterFlow> _outflowsBuffer;
  SimpleBuffer<int> _unsafeCellHeightsBuffer;

  void SetupBuffers() {
    _packedInput1Buffer = new SimpleBuffer<InputStruct1>(
        "PackedInput1", new InputStruct1[_totalMapSize]);
    _packedInput2Buffer = new SimpleBuffer<InputStruct2>(
        "PackedInput2", new InputStruct2[_totalMapSize]);
    //_packedInput2 = new InputStruct2[_totalMapSize];

    _waterDepthsBuffer = new SimpleBuffer<float>(
        "WaterDepthsBuff", _waterMap.WaterDepths);
    _contaminationsBuffer = new SimpleBuffer<float>(
        "ContaminationsBuff", _waterContaminationMap.Contaminations);
    _outflowsBuffer = new SimpleBuffer<WaterFlow>(
        "OutflowsBuff", _waterMap.Outflows);
    _unsafeCellHeightsBuffer = new SimpleBuffer<int>(
        "UnsafeCellHeightsBuff", _terrainMap._heights);

    _moistureLevelsBuff = new SimpleBuffer<float>(
        "MoistureLevelsBuff", _soilMoistureSimulator.MoistureLevels);
    _moistureLevelsChangedLastTickBuffer = new AppendBuffer<int>(
        "MoistureLevelsChangedLastTickBuff", new int[_totalMapSize]);
    _waterEvaporationModifierBuff = new SimpleBuffer<float>(
        "WaterEvaporationModifierBuff", new float[_totalMapSize]);

    _contaminationsChangedLastTickBuffer = new AppendBuffer<int>(
        "ContaminationsChangedLastTickBuff", new int[_totalMapSize]);
    _contaminationCandidatesBuffer = new SimpleBuffer<float>(
        "ContaminationCandidatesBuff", _soilContaminationSimulator._contaminationCandidates);
    _contaminationLevelsBuffer = new SimpleBuffer<float>(
        "ContaminationLevelsBuff", _soilContaminationSimulator.ContaminationLevels);
  }

  #endregion

  #region Pipeline running logic

  void TickSimulation() {
    PrepareInputs();

    _runShadersStopwatch.Start();
    _waterSimShaderPipeline.RunBlocking();
    _soilContaminationSimShaderPipeline.RunBlocking();
    _soilMoistureSimShaderPipeline.RunBlocking();

    // Wait for the GPU to finish the simulation. Only needed for accurate profiling.
    AsyncGPUReadback.Request(_waterDepthsBuffer.Buffer).WaitForCompletion();
    AsyncGPUReadback.Request(_moistureLevelsBuff.Buffer).WaitForCompletion();
    AsyncGPUReadback.Request(_contaminationsBuffer.Buffer).WaitForCompletion();
    
    _runShadersSampler.AddSample(_runShadersStopwatch.Elapsed.TotalSeconds);
    _runShadersStopwatch.Reset();

    UpdateOutputs();
  }

  void OnSimulationEnabled() {
    DebugEx.Warning("*** Enabling GPU simulation");
    _moistureLevelsBuff.PushToGpu(null);
    _contaminationCandidatesBuffer.PushToGpu(null);
    _contaminationLevelsBuffer.PushToGpu(null);
  }

  void OnSimulationDisabled() {
    DebugEx.Warning("*** Disabling GPU simulation");
    _contaminationCandidatesBuffer.PullFromGpu(null);
  }

  void PrepareInputs() {
    _prepareInputsStopwatch.Start();

    // Handle CPU part fo teh water simulator.
    _waterSimulator._deltaTime = _waterSimulator._fixedDeltaTime * _waterSimulator._waterSimulationSettings.TimeScale;
    _waterSimulator.UpdateWaterSources();
    _waterSimulator.UpdateWaterChanges();
    _waterDepthsBuffer.PushToGpu(null);
    _contaminationsBuffer.PushToGpu(null);

    _outflowsBuffer.PushToGpu(null);
    _unsafeCellHeightsBuffer.PushToGpu(null);

    //FIXME: React on the change events and don't update everything.
    //FIXME: Maybe deprecate packed inout all together? Need AMD testing.
    var packedInput1 = _packedInput1Buffer.Values;
    for (var index = packedInput1.Length - 1; index >= 0; index--) {
      uint bitmapFlags = 0;
      if (_soilBarrierMap.ContaminationBarriers[index]) {
        bitmapFlags |= InputStruct1.ContaminationBarrierBit;
      }
      if (_soilBarrierMap.AboveMoistureBarriers[index]) {
        bitmapFlags |= InputStruct1.AboveMoistureBarrierBit;
      }
      if (_soilBarrierMap.FullMoistureBarriers[index]) {
        bitmapFlags |= InputStruct1.FullMoistureBarrierBit;
      }
      if (DirectSoilMoistureSystemAccessor.MoistureLevelOverrides.ContainsKey(index)) {
        bitmapFlags |= InputStruct1.WaterTowerIrrigatedBit;
      }
      packedInput1[index] = new InputStruct1 {
          // Contaminations = _waterContaminationService.Contamination(index),
          // WaterDepths = _waterService.WaterDepth(index),
          // UnsafeCellHeights = _terrainService.UnsafeCellHeight(index),
          BitmapFlags = bitmapFlags,
      };
    }
    _packedInput1Buffer.PushToGpu(null);

    // FIXME: This data is not updated every frame. Make it event driven.
    for (var index = 0; index < _totalMapSize; index++) {
      uint bitmapFlags = 0;
      if (_waterSimulator._impermeableSurfaceService.PartialObstacles[index]) {
        bitmapFlags |= InputStruct2.PartialObstaclesBit;
      }
      if (_waterSimulator._mapIndexService.IndexIsInActualMap(index)) {
        // FIXME: This is a constant array that is filled once on game load.
        bitmapFlags |= InputStruct2.IsInActualMapBit;
      }
      _packedInput2Buffer.Values[index] = new InputStruct2 {
          ImpermeableSurfaceServiceHeight = _waterSimulator._impermeableSurfaceService.Heights[index],
          ImpermeableSurfaceServiceMinFlowSlower = _waterSimulator._impermeableSurfaceService.MinFlowSlowers[index],
          // FIXME: It's us who fills it (moisture sims). It's output only.
          EvaporationModifier = _waterSimulator._threadSafeWaterEvaporationMap.EvaporationModifiers[index],
          BitmapFlags = bitmapFlags,
      };
    }
    _packedInput2Buffer.PushToGpu(null);

    _moistureLevelsChangedLastTickBuffer.Initialize(null);
    _contaminationsChangedLastTickBuffer.Initialize(null);

    _prepareInputsSampler.AddSample(_prepareInputsStopwatch.Elapsed.TotalSeconds);
    _prepareInputsStopwatch.Reset();
  }

  void UpdateOutputs() {
    _updateOutputsStopwatch.Start();

    // Water sim.
    _waterDepthsBuffer.PullFromGpu(null);
    _contaminationsBuffer.PullFromGpu(null);
    _directWaterServiceAccessor.UpdateDepthsCallback(_waterSimulator._deltaTime);
    _waterSimulator._transientWaterMap.Refresh();
    _outflowsBuffer.PullFromGpu(null);

    // Soil moisture sim.
    _moistureLevelsBuff.PullFromGpu(null);

    _waterEvaporationModifierBuff.PullFromGpu(null);
    var waterEvaporationModifier = _waterEvaporationModifierBuff.Values;
    for (var index = waterEvaporationModifier.Length - 1; index >= 0; index--) {
      //FIXME: maybe write it directly?
      _soilMoistureSimulator._waterService.SetWaterEvaporationModifier(index, waterEvaporationModifier[index]);
    }

    _moistureLevelsChangedLastTickBuffer.PullFromGpu(null);
    var moistureLevelsChangedLastTick = _moistureLevelsChangedLastTickBuffer.Values;
    for (var i = _moistureLevelsChangedLastTickBuffer.DataLength - 1; i >= 0; i--) {
      _soilMoistureSimulator._moistureLevelsChangedLastTick.Add(
          _mapIndexService.IndexToCoordinates(moistureLevelsChangedLastTick[i]));
    }

    // Soil contamination sim.
    _contaminationLevelsBuffer.PullFromGpu(null);

    _contaminationsChangedLastTickBuffer.PullFromGpu(null);
    var contaminationsChangedLastTick = _contaminationsChangedLastTickBuffer.Values;
    for (var i = _contaminationsChangedLastTickBuffer.DataLength - 1; i >= 0; i--) {
      _soilContaminationSimulator._contaminationsChangedLastTick.Add(
          _mapIndexService.IndexToCoordinates(contaminationsChangedLastTick[i]));
    }

    _updateOutputsSampler.AddSample(_updateOutputsStopwatch.Elapsed.TotalSeconds);
    _updateOutputsStopwatch.Reset();
  }

  #endregion

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    _mapDataSize = new Vector3Int(_mapIndexService.MapSize.x, _mapIndexService.MapSize.y, 1);
    _mapIndexStride = _mapIndexService.Stride;
    _totalMapSize = _mapIndexService.TotalMapSize;
    SetupBuffers();
    SetupWaterSimulatorShader();
    SetupSoilContaminationShader();
    SetupSoilMoistureShader();
    new GameObject(GetType().FullName + "#FixedTicker").AddComponent<FixedUpdateListener>();
  }

  #endregion

  #region Shaders setup

  const string WaterSimulatorShaderName = "igorz.timbercommons/shaders/WaterSimulatorPacked";
  const string SoilMoistureSimulatorShaderName = "igorz.timbercommons/shaders/SoilMoistureSimulatorPacked";
  const string SoilContaminationSimulatorShaderName = "igorz.timbercommons/shaders/SoilContaminationSimulatorShaderPacked";

  ShaderPipeline _waterSimShaderPipeline;
  ShaderPipeline _soilMoistureSimShaderPipeline;
  ShaderPipeline _soilContaminationSimShaderPipeline;

  void SetupWaterSimulatorShader() {
    var waterSimulationSettings = _waterSimulator._waterSimulationSettings;
    var waterContaminationSimulationSettings = _waterSimulator._waterContaminationSimulationSettings;
    var shader = Object.Instantiate(_resourceAssetLoader.Load<ComputeShader>(WaterSimulatorShaderName));
    _waterSimShaderPipeline = ShaderPipeline.NewBuilder(shader)
        // Simulation settings.
        // Common.
        .WithConstantValue("Stride", _mapIndexStride)
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
        // FIXME: make it input.
        //.WithInputBuffer("PackedInput2", _packedInput2)
        .WithIntermediateBuffer(_packedInput2Buffer) // INPUT
        .WithIntermediateBuffer("TempOutflowsBuff", sizeof(float) * 4, _totalMapSize)
        .WithIntermediateBuffer("InitialWaterDepthsBuff", sizeof(float), _totalMapSize)
        .WithIntermediateBuffer("ContaminationsBufferBuff", sizeof(float), _totalMapSize)
        .WithIntermediateBuffer("ContaminationDiffusionsBuff", sizeof(float) * 4, _totalMapSize)
        .WithIntermediateBuffer(_outflowsBuffer)
        .WithIntermediateBuffer(_waterDepthsBuffer)
        .WithIntermediateBuffer(_contaminationsBuffer)
        // The kernel chain! They will execute in the order they are declared.
        .DispatchKernel(
            "SavePreviousState", new Vector3Int(_totalMapSize, 1, 1),
            "i:WaterDepthsBuff", "i:ContaminationsBuff",
            "o:InitialWaterDepthsBuff", "o:ContaminationsBufferBuff")
        .DispatchKernel(
            "UpdateOutflows", _mapDataSize,
            "i:PackedInput2", "i:WaterDepthsBuff", "i:OutflowsBuff",
            "o:TempOutflowsBuff")
        .DispatchKernel(
            "UpdateWaterParameters", _mapDataSize,
            "i:PackedInput2", "i:WaterDepthsBuff", "i:ContaminationsBuff", "i:OutflowsBuff",
            "i:TempOutflowsBuff", "i:InitialWaterDepthsBuff",
            "o:ContaminationsBufferBuff",
            "o:OutflowsBuff", "o:WaterDepthsBuff")
        .DispatchKernel(
            "SimulateContaminationDiffusion1", _mapDataSize,
            "i:PackedInput2", "i:WaterDepthsBuff",
            "i:TempOutflowsBuff",
            "o:ContaminationDiffusionsBuff")
        .DispatchKernel(
            "SimulateContaminationDiffusion2", _mapDataSize,
            "i:WaterDepthsBuff",
            "i:ContaminationsBufferBuff", "i:ContaminationDiffusionsBuff",
            "o:ContaminationsBuff")
        .Build();

    DebugEx.Warning("*** Water  shader execution plan:\n{0}", string.Join("\n", _waterSimShaderPipeline.GetExecutionPlan()));
  }

  void SetupSoilMoistureShader() {
    var simulationSettings = _soilMoistureSimulator._soilMoistureSimulationSettings;

    var shader = Object.Instantiate(_resourceAssetLoader.Load<ComputeShader>(SoilMoistureSimulatorShaderName));
    _soilMoistureSimShaderPipeline = ShaderPipeline.NewBuilder(shader)
        // Simulation settings.
        // Common.
        .WithConstantValue("Stride", _mapIndexStride)
        // SoilMoistureSimulationSettings
        .WithConstantValue("ConstantQuadraticCoefficient", simulationSettings.ConstantQuadraticCoefficient)
        .WithConstantValue("LinearQuadraticCoefficient", simulationSettings.LinearQuadraticCoefficient)
        .WithConstantValue("MaxClusterSaturation", simulationSettings.MaxClusterSaturation)
        .WithConstantValue("MinimumWaterContamination", simulationSettings.MinimumWaterContamination)
        .WithConstantValue("QuadraticEvaporationCoefficient", simulationSettings.QuadraticEvaporationCoefficient)
        .WithConstantValue("VerticalSpreadCostMultiplier", simulationSettings.VerticalSpreadCostMultiplier)
        // DirectSoilMoistureSimulationSettings
        .WithConstantValue("WaterTowerIrrigatedLevel", 1f)
        // Sim calculated.
        .WithConstantValue("WaterContaminationScaler", _soilMoistureSimulator._waterContaminationScaler)
        // Intermediate buffers.
        .WithIntermediateBuffer("LastTickMoistureLevelsBuff", sizeof(float), _totalMapSize)
        .WithIntermediateBuffer("WateredNeighboursBuff", sizeof(float), _totalMapSize)
        .WithIntermediateBuffer("ClusterSaturationBuff", sizeof(float), _totalMapSize)
        // Input buffers. They are pushed to the GPU every frame.
        .WithIntermediateBuffer(_packedInput1Buffer)
        .WithIntermediateBuffer(_waterDepthsBuffer)
        .WithIntermediateBuffer(_contaminationsBuffer)
        .WithIntermediateBuffer(_unsafeCellHeightsBuffer)
        // Output buffers. They are read from the GPU every frame.
        .WithIntermediateBuffer(_moistureLevelsBuff)  // Must be pushed on init.
        .WithIntermediateBuffer(_moistureLevelsChangedLastTickBuffer)
        .WithIntermediateBuffer(_waterEvaporationModifierBuff)
        // The kernel chain! They will execute in the order they are declared.
        .DispatchKernel(
            "SavePreviousState", new Vector3Int(_totalMapSize, 1, 1),
            "i:MoistureLevelsBuff",
            "o:LastTickMoistureLevelsBuff")
        .DispatchKernel(
            "CountWateredNeighbors", _mapDataSize,
            "i:PackedInput1",
            "i:WaterDepthsBuff", "i:LastTickMoistureLevelsBuff",
            "o:WateredNeighboursBuff")
        .DispatchKernel(
            "CalculateClusterSaturationAndWaterEvaporation", _mapDataSize,
            "i:PackedInput1",
            "i:WaterDepthsBuff", "i:WateredNeighboursBuff",
            "o:ClusterSaturationBuff", "o:WaterEvaporationModifierBuff")
        .DispatchKernel(
            "CalculateMoisture", _mapDataSize,
            "i:PackedInput1",
            "i:WaterDepthsBuff", "i:ContaminationsBuff", "i:UnsafeCellHeightsBuff", "i:LastTickMoistureLevelsBuff",
            "i:ClusterSaturationBuff",
            "o:MoistureLevelsBuff", "o:MoistureLevelsChangedLastTickBuff")
        .Build();

    DebugEx.Warning("*** Soil moisture shader execution plan:\n{0}", string.Join("\n", _soilMoistureSimShaderPipeline.GetExecutionPlan()));
  }

  void SetupSoilContaminationShader() {
    var simulationSettings = _soilContaminationSimulator._soilContaminationSimulationSettings;

    var shader = Object.Instantiate(_resourceAssetLoader.Load<ComputeShader>(SoilContaminationSimulatorShaderName));
    _soilContaminationSimShaderPipeline = ShaderPipeline.NewBuilder(shader)
        // Simulation settings.
        .WithConstantValue("Stride", _mapIndexStride)
        .WithConstantValue("DeltaTime", Time.fixedDeltaTime)
        .WithConstantValue("ContaminationDecayRate", simulationSettings.ContaminationDecayRate)
        .WithConstantValue(
            "ContaminationNegativeEqualizationRate", simulationSettings.ContaminationNegativeEqualizationRate)
        .WithConstantValue(
            "ContaminationPositiveEqualizationRate", simulationSettings.ContaminationPositiveEqualizationRate)
        .WithConstantValue("ContaminationScaler", _soilContaminationSimulator._contaminationScaler)
        .WithConstantValue("ContaminationSpreadingRate", simulationSettings.ContaminationSpreadingRate)
        .WithConstantValue("DiagonalSpreadCost", _soilContaminationSimulator._diagonalSpreadCost)
        .WithConstantValue("MinimumSoilContamination", SoilContaminationSimulator.MinimumSoilContamination)
        .WithConstantValue("MinimumWaterContamination", simulationSettings.MinimumWaterContamination)
        .WithConstantValue("RegularSpreadCost", _soilContaminationSimulator._regularSpreadCost)
        .WithConstantValue("VerticalCostModifier", _soilContaminationSimulator._verticalCostModifier)
        // Intermediate buffers. They are used to exchange data between the stages.
        .WithIntermediateBuffer("LastTickContaminationCandidatesBuff", sizeof(float), _totalMapSize)
        .WithIntermediateBuffer(_contaminationCandidatesBuffer)  // Must be pulled/pushed on (de)init.
        // Input buffers. They are pushed to the GPU every frame.
        .WithIntermediateBuffer(_packedInput1Buffer)
        .WithIntermediateBuffer(_waterDepthsBuffer)
        .WithIntermediateBuffer(_contaminationsBuffer)
        .WithIntermediateBuffer(_unsafeCellHeightsBuffer)
        // Output buffers. They are pulled from the GPU every frame.
        .WithIntermediateBuffer(_contaminationLevelsBuffer)  // Must be pushed on init.
        .WithIntermediateBuffer(_contaminationsChangedLastTickBuffer)
        // The kernel chain! They will execute in the order they are declared.
        .DispatchKernel(
            "SavePreviousState", new Vector3Int(_totalMapSize, 1, 1),
            "i:ContaminationCandidatesBuff", "o:LastTickContaminationCandidatesBuff")
        .DispatchKernel(
            "CalculateContaminationCandidates", _mapDataSize,
            "i:PackedInput1", "i:WaterDepthsBuff", "i:ContaminationsBuff", "i:UnsafeCellHeightsBuff",
            "i:LastTickContaminationCandidatesBuff",
            "o:ContaminationCandidatesBuff")
        .DispatchKernel(
            "UpdateContaminationsFromCandidates", _mapDataSize,
            "i:ContaminationLevelsBuff", "i:ContaminationCandidatesBuff",
            "o:ContaminationLevelsBuff", "o:ContaminationsChangedLastTickBuff")
        .Build();

    DebugEx.Warning("*** Soil moisture shader execution plan:\n{0}", string.Join("\n", _soilContaminationSimShaderPipeline.GetExecutionPlan()));
  }

  #endregion

  #region FixedUpdate ticking logic.

  void FixedUpdate() {
    if (SimulatorEnabled) {
      TickSimulation();
    }
  }

  sealed class FixedUpdateListener : MonoBehaviour {
    void FixedUpdate() {
      Self.FixedUpdate();
    }
  }

  #endregion
}

}
