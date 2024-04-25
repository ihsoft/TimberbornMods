// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Timberborn.AssetSystem;
using Timberborn.MapIndexSystem;
using Timberborn.SoilMoistureSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityDev.Utils.ShaderPipeline;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgorZ.TimberCommons.GpuSimulators {

sealed class GpuSoilMoistureSimulator : IGpuSimulatorStats {

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

    PrepareInputData();
    _shaderPipeline.RunBlocking();
    FlushOutputData();

    stopwatch.Stop();
    _totalSimPerfSampler.AddSample(stopwatch.Elapsed.TotalSeconds);
    stopwatch.Reset();
    _shaderPerfSampler.AddSample(_shaderPipeline.LastRunDuration.TotalSeconds);
  }

  #endregion

  #region Implementation

  const string SimulatorShaderName = "igorz.timbercommons/shaders/SoilMoistureSimulator";
  const float MinLevelChangePrecision = 0.0001f;

  readonly SoilMoistureSimulator _soilMoistureSimulator;
  readonly IResourceAssetLoader _resourceAssetLoader;
  readonly MapIndexService _mapIndexService;

  readonly ValueSampler _shaderPerfSampler = new(10);
  readonly ValueSampler _totalSimPerfSampler = new(10);

  // Inputs.
  float[] _contaminationBuff;
  float[] _waterDepthBuff;
  int[] _aboveMoistureBarriersBuff; //bool
  int[] _fullMoistureBarriersBuff; //bool
  int[] _ceiledWaterHeightBuff;
  int[] _unsafeCellHeightBuff;

  // Outputs.
  float[] _waterEvaporationModifier;
  int[] _moistureLevelsChangedLastTick;

  SimpleBuffer<float> _moistureLevelsBuff;
  AppendBuffer<int> _moistureLevelsChangedLastTickBuffer;

  ShaderPipeline _shaderPipeline;

  GpuSoilMoistureSimulator(SoilMoistureSimulator soilMoistureSimulator,
                           IResourceAssetLoader resourceAssetLoader, MapIndexService mapIndexService) {
    _soilMoistureSimulator = soilMoistureSimulator;
    _resourceAssetLoader = resourceAssetLoader;
    _mapIndexService = mapIndexService;
  }

  void SetupShader() {
    var simulationSettings = _soilMoistureSimulator._soilMoistureSimulationSettings;
    var totalMapSize = _mapIndexService.TotalMapSize;
    var mapDataSize = new Vector3Int(_mapIndexService.MapSize.x, _mapIndexService.MapSize.y, 1);

    _contaminationBuff = new float[totalMapSize];
    _waterDepthBuff = new float[totalMapSize];
    _aboveMoistureBarriersBuff = new int[totalMapSize];
    _fullMoistureBarriersBuff = new int[totalMapSize];
    _ceiledWaterHeightBuff = new int[totalMapSize];
    _unsafeCellHeightBuff = new int[totalMapSize];
    _waterEvaporationModifier = new float[totalMapSize];
    _moistureLevelsChangedLastTick = new int[totalMapSize];

    var prefab = _resourceAssetLoader.Load<ComputeShader>(SimulatorShaderName);
    var shader = Object.Instantiate(prefab);

    _moistureLevelsBuff = new SimpleBuffer<float>(
        "MoistureLevelsBuff", _soilMoistureSimulator.MoistureLevels);
    _moistureLevelsChangedLastTickBuffer = new AppendBuffer<int>(
        "MoistureLevelsChangedLastTickBuff", _moistureLevelsChangedLastTick);

    _shaderPipeline = ShaderPipeline.NewBuilder(shader)
        // Simulation settings.
        // Common.
        .WithConstantValue("MinLevelChange", MinLevelChangePrecision)
        .WithConstantValue("Stride", _mapIndexService.Stride)
        // SoilMoistureSimulationSettings
        .WithConstantValue("ConstantQuadraticCoefficient", simulationSettings.ConstantQuadraticCoefficient)
        .WithConstantValue("LinearQuadraticCoefficient", simulationSettings.LinearQuadraticCoefficient)
        .WithConstantValue("MaxClusterSaturation", simulationSettings.MaxClusterSaturation)
        .WithConstantValue("MinimumWaterContamination", simulationSettings.MinimumWaterContamination)
        .WithConstantValue("QuadraticEvaporationCoefficient", simulationSettings.QuadraticEvaporationCoefficient)
        .WithConstantValue("VerticalSpreadCostMultiplier", simulationSettings.VerticalSpreadCostMultiplier)
        // Sim calculated.
        .WithConstantValue("WaterContaminationScaler", _soilMoistureSimulator._waterContaminationScaler)
        // All buffers.
        .WithInputBuffer("ContaminationBuff", _contaminationBuff)
        .WithInputBuffer("WaterDepthBuff", _waterDepthBuff)
        .WithInputBuffer("AboveMoistureBarriersBuff", _aboveMoistureBarriersBuff)
        .WithInputBuffer("CeiledWaterHeightBuff", _ceiledWaterHeightBuff)
        .WithInputBuffer("FullMoistureBarriersBuff", _fullMoistureBarriersBuff)
        .WithInputBuffer("UnsafeCellHeightBuff", _unsafeCellHeightBuff)
        .WithIntermediateBuffer("LastTickMoistureLevelsBuff", typeof(float), totalMapSize)
        .WithIntermediateBuffer("WateredNeighboursBuff", typeof(float), totalMapSize)
        .WithIntermediateBuffer("ClusterSaturationBuff", typeof(float), totalMapSize)
        .WithOutputBuffer(_moistureLevelsBuff)
        .WithOutputBuffer(_moistureLevelsChangedLastTickBuffer)
        .WithOutputBuffer("WaterEvaporationModifierBuff", _waterEvaporationModifier)
        // The kernel chain! They will execute in the order they are declared.
        .DispatchKernel(
            "SavePreviousState997", new Vector3Int(totalMapSize, 1, 1),
            "i:MoistureLevelsBuff",
            "o:LastTickMoistureLevelsBuff")
        .DispatchKernel(
            "CountWateredNeighbors", mapDataSize,
            "s:WaterDepthBuff",
            "i:LastTickMoistureLevelsBuff",
            "o:WateredNeighboursBuff")
        .DispatchKernel(
            "CalculateClusterSaturationAndWaterEvaporation", mapDataSize,
            "s:WaterDepthBuff",
            "i:WateredNeighboursBuff",
            "o:ClusterSaturationBuff", "r:WaterEvaporationModifierBuff")
        .DispatchKernel(
            "CalculateMoisture", mapDataSize,
            "s:AboveMoistureBarriersBuff", "s:FullMoistureBarriersBuff", "s:UnsafeCellHeightBuff",
            "s:WaterDepthBuff", "s:ContaminationBuff", "s:CeiledWaterHeightBuff",
            "i:LastTickMoistureLevelsBuff", "i:ClusterSaturationBuff",
            "r:MoistureLevelsBuff", "r:MoistureLevelsChangedLastTickBuff")
        .Build();
    DebugEx.Warning("*** Shader execution plan:\n{0}", string.Join("\n", _shaderPipeline.GetExecutionPlan()));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void PrepareInputData() {
    var sim = _soilMoistureSimulator;
    for (var index = sim.MoistureLevels.Length - 1; index >= 0; index--) {
      _contaminationBuff[index] = sim._waterContaminationService.Contamination(index);
      _waterDepthBuff[index] = sim._waterService.WaterDepth(index);
      _aboveMoistureBarriersBuff[index] = sim._soilBarrierMap.AboveMoistureBarriers[index] ? 1 : 0;
      _fullMoistureBarriersBuff[index] = sim._soilBarrierMap.FullMoistureBarriers[index] ? 1 : 0;
      _ceiledWaterHeightBuff[index] = sim._waterService.CeiledWaterHeight(index);
      _unsafeCellHeightBuff[index] = sim._terrainService.UnsafeCellHeight(index);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void FlushOutputData() {
    var sim = _soilMoistureSimulator;
    for (var index = sim.MoistureLevels.Length - 1; index >= 0; index--) {
      sim._waterService.SetWaterEvaporationModifier(index, _waterEvaporationModifier[index]);
    }
    for (var i = _moistureLevelsChangedLastTickBuffer.DataLength - 1; i >= 0; i--) {
      sim._moistureLevelsChangedLastTick.Add(_mapIndexService.IndexToCoordinates(_moistureLevelsChangedLastTick[i]));
    }
  }

  void EnableSimulator() {
    DebugEx.Warning("*** Enabling moisture GPU sim");
    _moistureLevelsBuff.PushToGpu(null);
  }

  void DisableSimulator() {
    DebugEx.Warning("*** Disabling moisture GPU sim");
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
