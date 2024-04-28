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
sealed class GpuSoilContaminationSimulator : IGpuSimulatorStats {

  const string SimulatorShaderName = "igorz.timbercommons/shaders/SoilContaminationSimulatorShaderPacked";

  readonly SoilContaminationSimulator _soilContaminationSimulator;
  readonly IResourceAssetLoader _resourceAssetLoader;
  readonly MapIndexService _mapIndexService;

  readonly ValueSampler _shaderPerfSampler = new(10);
  readonly ValueSampler _totalSimPerfSampler = new(10);

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

  #endregion

  ShaderPipeline _shaderPipeline;

  GpuSoilContaminationSimulator(SoilContaminationSimulator soilContaminationSimulator,
                                 IResourceAssetLoader resourceAssetLoader, MapIndexService mapIndexService) {
    _soilContaminationSimulator = soilContaminationSimulator;
    _resourceAssetLoader = resourceAssetLoader;
    _mapIndexService = mapIndexService;
  }

  // ReSharper disable NotAccessedField.Local
  struct InputStruct1 {
    public float Contamination;
    public int UnsafeCellHeight;
    public int CeiledWaterHeight;
    public uint Bitmap;

    public const uint ContaminationBarrierBit = 0x0001;
    public const uint AboveMoistureBarrierBit = 0x0002;
  };
  // ReSharper restore NotAccessedField.Local

  InputStruct1[] _packedInput1;

  AppendBuffer<int> _contaminationsChangedLastTickBuffer;
  int[] _contaminationsChangedLastTick;
  SimpleBuffer<float> _contaminationCandidatesBuffer;
  SimpleBuffer<float> _contaminationLevelsBuffer;

  public void Initialize() {
    var simulationSettings = _soilContaminationSimulator._soilContaminationSimulationSettings;
    var totalMapSize = _mapIndexService.TotalMapSize;
    var mapDataSize = new Vector3Int(_mapIndexService.MapSize.x, _mapIndexService.MapSize.y, 1);

    _packedInput1 = new InputStruct1[totalMapSize];
    _contaminationsChangedLastTick = new int[totalMapSize];
    _contaminationsChangedLastTickBuffer = new AppendBuffer<int>(
        "ContaminationsChangedLastTick", _contaminationsChangedLastTick);
    _contaminationCandidatesBuffer = new SimpleBuffer<float>(
        "ContaminationCandidates", _soilContaminationSimulator._contaminationCandidates);
    _contaminationLevelsBuffer = new SimpleBuffer<float>(
        "ContaminationLevels", _soilContaminationSimulator.ContaminationLevels);

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
        // All buffers.
        .WithInputBuffer("PackedInput1", _packedInput1)
        .WithIntermediateBuffer(_contaminationCandidatesBuffer)
        .WithIntermediateBuffer("LastTickContaminationCandidates", sizeof(float), totalMapSize)
        .WithOutputBuffer(_contaminationLevelsBuffer)
        .WithOutputBuffer(_contaminationsChangedLastTickBuffer)
        // The kernel chain! They will execute in the order they are declared.
        .DispatchKernel(
            "SavePreviousState", new Vector3Int(totalMapSize, 1, 1),
            "i:ContaminationCandidates", "o:LastTickContaminationCandidates")
        .DispatchKernel(
            "CalculateContaminationCandidates", mapDataSize,
            "i:LastTickContaminationCandidates", "s:PackedInput1",
            "o:ContaminationCandidates")
        .DispatchKernel(
            "UpdateContaminationsFromCandidates", mapDataSize,
            "i:ContaminationLevels", "i:ContaminationCandidates",
            "r:ContaminationLevels", "r:ContaminationsChangedLastTick")
        .Build();
  }

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

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void PrepareInputData() {
    var sim = _soilContaminationSimulator;
    for (var i = _packedInput1.Length - 1; i >= 0; i--) {
      var bitmap =
          (sim._soilBarrierMap.ContaminationBarriers[i] ? InputStruct1.ContaminationBarrierBit : 0)
          | (sim._soilBarrierMap.AboveMoistureBarriers[i] ? InputStruct1.AboveMoistureBarrierBit : 0);
      _packedInput1[i] = new InputStruct1 {
          Contamination = sim._waterContaminationService.Contamination(i),
          UnsafeCellHeight = sim._terrainService.UnsafeCellHeight(i),
          CeiledWaterHeight = sim._waterService.CeiledWaterHeight(i),
          Bitmap = bitmap,
      };
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void FlushOutputData() {
    for (var i = _contaminationsChangedLastTickBuffer.DataLength - 1; i >= 0; i--) {
      _soilContaminationSimulator._contaminationsChangedLastTick.Add(
          _mapIndexService.IndexToCoordinates(_contaminationsChangedLastTick[i]));
    }
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
