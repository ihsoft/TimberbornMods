// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics;
using System.Runtime.CompilerServices;
using IgorZ.TimberCommons.WaterService;
using Timberborn.AssetSystem;
using Timberborn.MapIndexSystem;
using Timberborn.SoilMoistureSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityDev.Utils.ShaderPipeline;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgorZ.TimberCommons.GpuSimulators {

sealed class GpuSoilMoistureSimulator {

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
  public void Initialize(GpuSimulatorsController gpuSimulatorsController) {
    SetupShader(gpuSimulatorsController);
  } 

  /// <summary>Executes the logic and updates the stock simulators with the processed data.</summary>
  public void TickPipeline() {
    _stopwatch.Restart();
    _shaderPipeline.RunBlocking();
    _stopwatch.Stop();
    TotalSimPerfSampler.AddSample(_stopwatch.Elapsed.TotalSeconds);
    ShaderPerfSampler.AddSample(_shaderPipeline.LastRunDuration.TotalSeconds);
  }
  readonly Stopwatch _stopwatch = new();

  internal readonly ValueSampler ShaderPerfSampler = new(10);
  internal readonly ValueSampler TotalSimPerfSampler = new(10);

  #endregion

  #region Implementation

  const string SimulatorShaderName = "igorz.timbercommons/shaders/SoilMoistureSimulatorPacked";

  readonly SoilMoistureSimulator _soilMoistureSimulator;
  readonly IResourceAssetLoader _resourceAssetLoader;
  readonly MapIndexService _mapIndexService;

  SimpleBuffer<float> _moistureLevelsBuff;
  SimpleBuffer<float> _waterEvaporationModifierBuff;
  AppendBuffer<int> _moistureLevelsChangedLastTickBuffer;
  BaseBuffer _packedInputBuffer;

  ShaderPipeline _shaderPipeline;

  GpuSoilMoistureSimulator(SoilMoistureSimulator soilMoistureSimulator,
                           IResourceAssetLoader resourceAssetLoader, MapIndexService mapIndexService) {
    _soilMoistureSimulator = soilMoistureSimulator;
    _resourceAssetLoader = resourceAssetLoader;
    _mapIndexService = mapIndexService;
  }

  void SetupShader(GpuSimulatorsController gpuSimulatorsController) {
    var simulationSettings = _soilMoistureSimulator._soilMoistureSimulationSettings;
    var totalMapSize = _mapIndexService.TotalMapSize;
    var mapDataSize = new Vector3Int(_mapIndexService.MapSize.x, _mapIndexService.MapSize.y, 1);

    var prefab = _resourceAssetLoader.Load<ComputeShader>(SimulatorShaderName);
    var shader = Object.Instantiate(prefab);

    _moistureLevelsBuff = new SimpleBuffer<float>(
        "MoistureLevelsBuff", _soilMoistureSimulator.MoistureLevels);
    _moistureLevelsChangedLastTickBuffer = new AppendBuffer<int>(
        "MoistureLevelsChangedLastTickBuff", new int[totalMapSize]);
    _packedInputBuffer = gpuSimulatorsController.PackedPersistentValuesBuffer1;
    _waterEvaporationModifierBuff = new SimpleBuffer<float>(
        "WaterEvaporationModifierBuff", new float[totalMapSize]);

    _shaderPipeline = ShaderPipeline.NewBuilder(shader)
        // Simulation settings.
        // Common.
        .WithConstantValue("Stride", _mapIndexService.Stride)
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
        .WithIntermediateBuffer("LastTickMoistureLevelsBuff", sizeof(float), totalMapSize)
        .WithIntermediateBuffer("WateredNeighboursBuff", sizeof(float), totalMapSize)
        .WithIntermediateBuffer("ClusterSaturationBuff", sizeof(float), totalMapSize)
        // Input buffers. They are pushed to the GPU every frame.
        .WithIntermediateBuffer(_packedInputBuffer)
        // Output buffers. They are read from the GPU every frame.
        .WithIntermediateBuffer(_moistureLevelsBuff)  // Must be pushed on init.
        .WithIntermediateBuffer(_moistureLevelsChangedLastTickBuffer)
        .WithIntermediateBuffer(_waterEvaporationModifierBuff)
        // The kernel chain! They will execute in the order they are declared.
        .DispatchKernel(
            "SavePreviousState", new Vector3Int(totalMapSize, 1, 1),
            "i:MoistureLevelsBuff",
            "o:LastTickMoistureLevelsBuff")
        .DispatchKernel(
            "CountWateredNeighbors", mapDataSize,
            "i:PackedInput1",
            "i:LastTickMoistureLevelsBuff",
            "o:WateredNeighboursBuff")
        .DispatchKernel(
            "CalculateClusterSaturationAndWaterEvaporation", mapDataSize,
            "i:PackedInput1",
            "i:WateredNeighboursBuff",
            "o:ClusterSaturationBuff", "o:WaterEvaporationModifierBuff")
        .DispatchKernel(
            "CalculateMoisture", mapDataSize,
            "i:PackedInput1",
            "i:LastTickMoistureLevelsBuff", "i:ClusterSaturationBuff",
            "o:MoistureLevelsBuff", "o:MoistureLevelsChangedLastTickBuff")
        .Build();
    DebugEx.Warning("*** Shader execution plan:\n{0}", string.Join("\n", _shaderPipeline.GetExecutionPlan()));
  }

  public void ProcessOutput() {
    var sim = _soilMoistureSimulator;

    _moistureLevelsBuff.PullFromGpu(null);

    _waterEvaporationModifierBuff.PullFromGpu(null);
    var waterEvaporationModifier = _waterEvaporationModifierBuff.Values;
    for (var index = sim.MoistureLevels.Length - 1; index >= 0; index--) {
      //FIXME: maybe write it directly?
      sim._waterService.SetWaterEvaporationModifier(index, waterEvaporationModifier[index]);
    }

    _moistureLevelsChangedLastTickBuffer.PullFromGpu(null);
    var moistureLevelsChangedLastTick = _moistureLevelsChangedLastTickBuffer.Values;
    for (var index = _moistureLevelsChangedLastTickBuffer.DataLength - 1; index >= 0; index--) {
      sim._moistureLevelsChangedLastTick.Add(_mapIndexService.IndexToCoordinates(moistureLevelsChangedLastTick[index]));
    }
    _moistureLevelsChangedLastTickBuffer.Initialize(null);
  }

  void EnableSimulator() {
    DebugEx.Warning("*** Enabling moisture GPU sim-2");
    _moistureLevelsBuff.PushToGpu(null);
  }

  void DisableSimulator() {
    DebugEx.Warning("*** Disabling moisture GPU sim-2");
  }

  #endregion
}

}
