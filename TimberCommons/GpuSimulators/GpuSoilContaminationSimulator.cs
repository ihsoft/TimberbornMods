// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Timberborn.AssetSystem;
using Timberborn.MapIndexSystem;
using Timberborn.SoilContaminationSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityDev.Utils.ShaderPipeline;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgorZ.TimberCommons.GpuSimulators {

/// <summary>
/// This class completely replaces the logic previously handled by the <see cref="SoilContaminationSimulator"/> class.
/// </summary>
/// <remarks>
/// The simulation is now performed on the GPU using a compute shader. A notable difference from the 
/// <see cref="SoilContaminationSimulator"/> class is that the simulation now occurs within the Unity 
/// `FixedUpdate` method. The stock code is not used at all!
/// </remarks>
/// <seealso cref="SoilContaminationSimulatorTickSimulationPatch"/>
sealed class GpuSoilContaminationSimulator {

  const string SimulatorShaderName = "igorz.timbercommons/shaders/SoilContaminationSimulatorShaderPacked";

  readonly SoilContaminationSimulator _soilContaminationSimulator;
  readonly IResourceAssetLoader _resourceAssetLoader;
  readonly MapIndexService _mapIndexService;

  #region API

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

  public void Initialize(GpuSimulatorsController gpuSimulatorsController) {
    SetupShader(gpuSimulatorsController);
  }

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

  ShaderPipeline _shaderPipeline;

  GpuSoilContaminationSimulator(SoilContaminationSimulator soilContaminationSimulator,
                                 IResourceAssetLoader resourceAssetLoader, MapIndexService mapIndexService) {
    _soilContaminationSimulator = soilContaminationSimulator;
    _resourceAssetLoader = resourceAssetLoader;
    _mapIndexService = mapIndexService;
  }

  AppendBuffer<int> _contaminationsChangedLastTickBuffer;
  SimpleBuffer<float> _contaminationCandidatesBuffer;
  BaseBuffer _packedInputBuffer;
  SimpleBuffer<float> _contaminationLevelsBuffer;

  void SetupShader(GpuSimulatorsController gpuSimulatorsController) {
    var simulationSettings = _soilContaminationSimulator._soilContaminationSimulationSettings;
    var totalMapSize = _mapIndexService.TotalMapSize;
    var mapDataSize = new Vector3Int(_mapIndexService.MapSize.x, _mapIndexService.MapSize.y, 1);

    _contaminationsChangedLastTickBuffer = new AppendBuffer<int>(
        "ContaminationsChangedLastTickBuff", new int[totalMapSize]);
    _contaminationCandidatesBuffer = new SimpleBuffer<float>(
        "ContaminationCandidatesBuff", _soilContaminationSimulator._contaminationCandidates);
    _packedInputBuffer = gpuSimulatorsController.PackedPersistentValuesBuffer1;
    _contaminationLevelsBuffer = new SimpleBuffer<float>(
        "ContaminationLevelsBuff", _soilContaminationSimulator.ContaminationLevels);

    var prefab = _resourceAssetLoader.Load<ComputeShader>(SimulatorShaderName);
    var shader = Object.Instantiate(prefab);

    _shaderPipeline = ShaderPipeline.NewBuilder(shader)
        // Simulation settings.
        .WithConstantValue("Stride", _mapIndexService.Stride)
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
        // Intermediate buffers.
        .WithIntermediateBuffer("LastTickContaminationCandidatesBuff", sizeof(float), totalMapSize)
        .WithIntermediateBuffer(_contaminationCandidatesBuffer)  // Must be pulled/pushed on (de)init.
        // Input buffers. They are pushed to the GPU every frame.
        .WithIntermediateBuffer(_packedInputBuffer)
        // Output buffers. They are pulled from the GPU every frame.
        .WithIntermediateBuffer(_contaminationLevelsBuffer)   // Must be pushed on init.
        .WithIntermediateBuffer(_contaminationsChangedLastTickBuffer)
        // The kernel chain! They will execute in the order they are declared.
        .DispatchKernel(
            "SavePreviousState", new Vector3Int(totalMapSize, 1, 1),
            "i:ContaminationCandidatesBuff", "o:LastTickContaminationCandidatesBuff")
        .DispatchKernel(
            "CalculateContaminationCandidates", mapDataSize,
            "i:PackedInput1", "i:LastTickContaminationCandidatesBuff",
            "o:ContaminationCandidatesBuff")
        .DispatchKernel(
            "UpdateContaminationsFromCandidates", mapDataSize,
            "i:ContaminationLevelsBuff", "i:ContaminationCandidatesBuff",
            "o:ContaminationLevelsBuff", "o:ContaminationsChangedLastTickBuff")
        .Build();
  }

  public void ProcessOutput() {
    _contaminationLevelsBuffer.PullFromGpu(null);

    _contaminationsChangedLastTickBuffer.PullFromGpu(null);
    var contaminationsChangedLastTick = _contaminationsChangedLastTickBuffer.Values;
    for (var index = _contaminationsChangedLastTickBuffer.DataLength - 1; index >= 0; index--) {
      _soilContaminationSimulator._contaminationsChangedLastTick.Add(
          _mapIndexService.IndexToCoordinates(contaminationsChangedLastTick[index]));
    }
    _contaminationsChangedLastTickBuffer.Initialize(null);
  }

  void EnableSimulator() {
    DebugEx.Warning("*** Enabling GPU sim-2");
    _contaminationCandidatesBuffer.PushToGpu(null);
    _contaminationLevelsBuffer.PushToGpu(null);
  }

  void DisableSimulator() {
    DebugEx.Warning("*** Disabling GPU sim-2");
    _contaminationCandidatesBuffer.PullFromGpu(null);
  }

  #endregion
}

}
