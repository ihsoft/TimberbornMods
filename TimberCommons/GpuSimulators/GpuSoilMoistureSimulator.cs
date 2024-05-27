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
  public void Initialize() {
    SetupShader();
  } 

  /// <summary>Executes the logic and updates the stock simulators with the processed data.</summary>
  public void TickPipeline() {
    _stopwatch.Restart();

    PrepareInputData();
    _shaderPipeline.RunBlocking();
    FlushOutputData();

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
  const float MinLevelChangePrecision = 0.0001f;

  readonly SoilMoistureSimulator _soilMoistureSimulator;
  readonly IResourceAssetLoader _resourceAssetLoader;
  readonly MapIndexService _mapIndexService;

  // Inputs.
  // ReSharper disable NotAccessedField.Local
  struct InputStruct1 {
    public float Contamination;
    public float WaterDepth;
    public int UnsafeCellHeight;
    public uint BitmapFlags;

    public const uint ContaminationBarrierBit = 0x0001;
    public const uint AboveMoistureBarrierBit = 0x0002;
    public const uint FullMoistureBarrierBit = 0x0004;
    public const uint WaterTowerIrrigatedBit = 0x0008;
  };
  // ReSharper restore NotAccessedField.Local

  InputStruct1[] _packedInput1;

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

    _packedInput1 = new InputStruct1[totalMapSize];
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
        // DirectSoilMoistureSimulationSettings
        .WithConstantValue("WaterTowerIrrigatedLevel", 1f)
        // Sim calculated.
        .WithConstantValue("WaterContaminationScaler", _soilMoistureSimulator._waterContaminationScaler)
        // All buffers.
        .WithInputBuffer("PackedInput1", _packedInput1)
        .WithIntermediateBuffer("LastTickMoistureLevelsBuff", sizeof(float), totalMapSize)
        .WithIntermediateBuffer("WateredNeighboursBuff", sizeof(float), totalMapSize)
        .WithIntermediateBuffer("ClusterSaturationBuff", sizeof(float), totalMapSize)
        .WithOutputBuffer(_moistureLevelsBuff)
        .WithOutputBuffer(_moistureLevelsChangedLastTickBuffer)
        .WithOutputBuffer("WaterEvaporationModifierBuff", _waterEvaporationModifier)
        // The kernel chain! They will execute in the order they are declared.
        .DispatchKernel(
            "SavePreviousState", new Vector3Int(totalMapSize, 1, 1),
            "i:MoistureLevelsBuff",
            "o:LastTickMoistureLevelsBuff")
        .DispatchKernel(
            "CountWateredNeighbors", mapDataSize,
            "s:PackedInput1",
            "i:LastTickMoistureLevelsBuff",
            "o:WateredNeighboursBuff")
        .DispatchKernel(
            "CalculateClusterSaturationAndWaterEvaporation", mapDataSize,
            "s:PackedInput1",
            "i:WateredNeighboursBuff",
            "o:ClusterSaturationBuff", "r:WaterEvaporationModifierBuff")
        .DispatchKernel(
            "CalculateMoisture", mapDataSize,
            "s:PackedInput1",
            "i:LastTickMoistureLevelsBuff", "i:ClusterSaturationBuff",
            "r:MoistureLevelsBuff", "r:MoistureLevelsChangedLastTickBuff")
        .Build();
    DebugEx.Warning("*** Shader execution plan:\n{0}", string.Join("\n", _shaderPipeline.GetExecutionPlan()));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void PrepareInputData() {
    var sim = _soilMoistureSimulator;
    for (var index = _packedInput1.Length - 1; index >= 0; index--) {
      uint bitmapFlags = 0;
      if (sim._soilBarrierMap.ContaminationBarriers[index]) {
        bitmapFlags |= InputStruct1.ContaminationBarrierBit;
      }
      if (sim._soilBarrierMap.AboveMoistureBarriers[index]) {
        bitmapFlags |= InputStruct1.AboveMoistureBarrierBit;
      }
      if (sim._soilBarrierMap.FullMoistureBarriers[index]) {
        bitmapFlags |= InputStruct1.FullMoistureBarrierBit;
      }
      if (DirectSoilMoistureSystemAccessor.MoistureLevelOverrides.ContainsKey(index)) {
        bitmapFlags |= InputStruct1.WaterTowerIrrigatedBit;
      }
      _packedInput1[index] = new InputStruct1 {
          Contamination = sim._waterContaminationService.Contamination(index),
          WaterDepth = sim._waterService.WaterDepth(index),
          UnsafeCellHeight = sim._terrainService.UnsafeCellHeight(index),
          BitmapFlags = bitmapFlags,
      };
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void FlushOutputData() {
    var sim = _soilMoistureSimulator;
    for (var index = sim.MoistureLevels.Length - 1; index >= 0; index--) {
      //FIXME: maybe write it directly?
      sim._waterService.SetWaterEvaporationModifier(index, _waterEvaporationModifier[index]);
    }
    for (var i = _moistureLevelsChangedLastTickBuffer.DataLength - 1; i >= 0; i--) {
      sim._moistureLevelsChangedLastTick.Add(_mapIndexService.IndexToCoordinates(_moistureLevelsChangedLastTick[i]));
    }
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
