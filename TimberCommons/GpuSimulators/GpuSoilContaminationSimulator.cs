// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Timberborn.AssetSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoilContaminationSystem;
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
sealed class GpuSoilContaminationSimulator : IPostLoadableSingleton, IGpuSimulatorStats {

  const string SimulatorShaderName = "igorz.timbercommons/shaders/SoilContaminationSimulatorShader";

  readonly SoilContaminationSimulator _soilContaminationSimulator;
  readonly IResourceAssetLoader _resourceAssetLoader;

  readonly ValueSampler _shaderPerfSampler = new(10);
  readonly ValueSampler _totalSimPerfSampler = new(10);

  internal static GpuSoilContaminationSimulator Self;
  internal bool IsEnabled;

  ShaderPipeline _shaderPipeline;
  int[] _ceiledWaterHeight;
  int[] _unsafeCellHeight;
  float[] _contamination;
  uint[] _contaminationBarriers;
  uint[] _aboveMoistureBarriers;
  float[] _contaminationLevels;

  GpuSoilContaminationSimulator(SoilContaminationSimulator soilContaminationSimulator,
                                IResourceAssetLoader resourceAssetLoader) {
    _soilContaminationSimulator = soilContaminationSimulator;
    _resourceAssetLoader = resourceAssetLoader;
    Self = this;
  }

  void SetupShaderPipeline() {
    var simulationSettings = _soilContaminationSimulator._soilContaminationSimulationSettings;
    var indexService = _soilContaminationSimulator._mapIndexService;
    var totalMapSize = indexService.TotalMapSize;
    var mapDataSize = new Vector3Int(indexService.MapSize.x, indexService.MapSize.y, 1);

    _contaminationLevels = new float[totalMapSize];
    _ceiledWaterHeight = new int[totalMapSize];
    _unsafeCellHeight = new int[totalMapSize];
    _contamination = new float[totalMapSize];
    _contaminationBarriers = new uint[totalMapSize];
    _aboveMoistureBarriers = new uint[totalMapSize];

    var prefab = _resourceAssetLoader.Load<ComputeShader>(SimulatorShaderName);
    var shader = Object.Instantiate(prefab);

    _shaderPipeline = ShaderPipeline.NewBuilder(shader)
        // Simulation settings.
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
        .WithConstantValue("Stride", indexService.Stride)
        .WithConstantValue("VerticalCostModifier", _soilContaminationSimulator._verticalCostModifier)
        // All buffers.
        .WithInputBuffer("Contamination", _contamination)
        .WithInputBuffer("ContaminationBarriers", _contaminationBarriers)
        .WithInputBuffer("AboveMoistureBarriers", _aboveMoistureBarriers)
        .WithInputBuffer("CeiledWaterHeight", _ceiledWaterHeight)
        .WithInputBuffer("UnsafeCellHeight", _unsafeCellHeight)
        .WithIntermediateBuffer("LastTickContaminationCandidates", typeof(float), totalMapSize)
        .WithOutputBuffer("ContaminationCandidates", _soilContaminationSimulator._contaminationCandidates)
        .WithOutputBuffer("ContaminationLevels", _soilContaminationSimulator.ContaminationLevels)
        // The kernel chain!
        .DispatchKernel(
            "SavePreviousState", new Vector3Int(totalMapSize, 1, 1),
            "s:ContaminationCandidates", "o:LastTickContaminationCandidates")
        .DispatchKernel(
            "CalculateContaminationCandidates", mapDataSize,
            "i:LastTickContaminationCandidates", "s:Contamination", "s:ContaminationBarriers", "s:UnsafeCellHeight",
            "s:AboveMoistureBarriers", "s:CeiledWaterHeight",
            "r:ContaminationCandidates")
        .DispatchKernel(
            "UpdateContaminationsFromCandidates", mapDataSize,
            "i:ContaminationCandidates", "r:ContaminationLevels")
        .Build();
  }

  void TickPipeline() {
    var stopwatch = Stopwatch.StartNew();

    Array.Copy(_soilContaminationSimulator.ContaminationLevels, _contaminationLevels, _contaminationLevels.Length);
    PrepareInputData();
    _shaderPipeline.RunBlocking();
    for (var i = _contaminationLevels.Length - 1; i >= 0; i--) {
      if (_soilContaminationSimulator.ContaminationLevels[i] != _contaminationLevels[i]) {
        _soilContaminationSimulator._contaminationsChangedLastTick.Add(
            _soilContaminationSimulator._mapIndexService.IndexToCoordinates(i));
      }
    }

    stopwatch.Stop();
    _totalSimPerfSampler.AddSample(stopwatch.Elapsed.TotalSeconds);
    stopwatch.Reset();
    _shaderPerfSampler.AddSample(_shaderPipeline.LastRunDuration.TotalSeconds);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void PrepareInputData() {
    for (var i = _contamination.Length - 1; i >= 0; i--) {
      _contamination[i] = _soilContaminationSimulator._waterContaminationService.Contamination(i);
      _unsafeCellHeight[i] = _soilContaminationSimulator._terrainService.UnsafeCellHeight(i);
      _contaminationBarriers[i] = _soilContaminationSimulator._soilBarrierMap.ContaminationBarriers[i] ? 1u : 0u;
      _aboveMoistureBarriers[i] = _soilContaminationSimulator._soilBarrierMap.AboveMoistureBarriers[i] ? 1u : 0u;
      _ceiledWaterHeight[i] = _soilContaminationSimulator._waterService.CeiledWaterHeight(i);
    }
  }

  #region A helper class whose sole role is to deliver FixedUpdate to the singleton.

  sealed class FixedUpdateListener : MonoBehaviour {
    void FixedUpdate() {
      if (Self.IsEnabled){
        Self.TickPipeline();
      }
    }

    void OnDestroy() {
      Self._shaderPipeline.Dispose();
    }
  }

  #endregion

  #region IPostLoadableSingleton

  /// <summary>Creates a fixed update listener and initializes shader.</summary>
  public void PostLoad() {
    new GameObject(GetType().FullName + "#FixedTicker").AddComponent<FixedUpdateListener>();
    SetupShaderPipeline();
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
