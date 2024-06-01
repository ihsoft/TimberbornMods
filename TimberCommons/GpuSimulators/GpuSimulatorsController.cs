// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics;
using IgorZ.TimberCommons.WaterService;
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

  readonly GpuSoilContaminationSimulator _contaminationSimulator;
  readonly GpuSoilMoistureSimulator _moistureSimulator;
  readonly GpuWaterSimulator _waterSimulator;
  readonly MapIndexService _mapIndexService;
  readonly SoilBarrierMap _soilBarrierMap;
  readonly ITerrainService _terrainService;
  readonly IWaterContaminationService _waterContaminationService;
  readonly IWaterService _waterService;

  readonly WaterSimulationController _waterSimulationController;
  readonly SoilMoistureSimulationController _soilMoistureSimulationController;
  readonly SoilContaminationSimulationController _soilContaminationSimulationController;

  readonly Stopwatch _stopwatch = new();
  readonly ValueSampler _fixedUpdateSampler = new(10);

  internal static GpuSimulatorsController Self;
  internal bool SimulatorEnabled { get; private set; }

  GpuSimulatorsController(
      GpuSoilContaminationSimulator contaminationSimulator,
      GpuSoilMoistureSimulator moistureSimulator,
      GpuWaterSimulator waterSimulator,
      MapIndexService mapIndexService,
      SoilBarrierMap soilBarrierMap,
      ITerrainService terrainService,
      IWaterContaminationService waterContaminationService,
      IWaterService waterService,
      IWaterSimulationController waterSimulationController,
      ISoilMoistureSimulationController soilMoistureSimulationController,
      ISoilContaminationSimulationController soilContaminationSimulationController) {
    _contaminationSimulator = contaminationSimulator;
    _moistureSimulator = moistureSimulator;
    _waterSimulator = waterSimulator;
    _mapIndexService = mapIndexService;
    _soilBarrierMap = soilBarrierMap;
    _terrainService = terrainService;
    _waterContaminationService = waterContaminationService;
    _waterService = waterService;
    _waterSimulationController = (WaterSimulationController) waterSimulationController;
    _soilMoistureSimulationController = (SoilMoistureSimulationController) soilMoistureSimulationController;
    _soilContaminationSimulationController =
        (SoilContaminationSimulationController) soilContaminationSimulationController;
    Self = this;
  }
  
  internal void EnableSimulator(bool state) {
    SimulatorEnabled = state;
    DebugEx.Warning("*** GPU simulator state: {0}", SimulatorEnabled);

    if (state) {
      _waterSimulationController.LastTickDurationMs = 0;
      _soilMoistureSimulationController.LastTickDurationMs = 0;
      _soilContaminationSimulationController.LastTickDurationMs = 0;
      _contaminationSimulator.EnableSimulator();
      _moistureSimulator.EnableSimulator();
      _waterSimulator.EnableSimulator();
    } else {
      _contaminationSimulator.DisableSimulator();
      _moistureSimulator.DisableSimulator();
      _waterSimulator.DisableSimulator();
    }
  }

  internal string GetStatsText() {
    if (!SimulatorEnabled) {
      return "GPU simulation disabled.";
    }
    var text = new List<string>(15);
    {
      var (_, _, _, total) = _contaminationSimulator.TotalSimPerfSampler.GetStats();
      text.Add($"Soil contamination total: {total * 1000:0.##} ms");
      var (_, _, _, shader) = _contaminationSimulator.ShaderPerfSampler.GetStats();
      text.Add($"Soil contamination shader: {shader * 1000:0.##} ms");
    }
    {
      var (_, _, _, total) = _moistureSimulator.TotalSimPerfSampler.GetStats();
      text.Add($"Soil moisture total: {total * 1000:0.##} ms");
      var (_, _, _, shader) = _moistureSimulator.ShaderPerfSampler.GetStats();
      text.Add($"Soil moisture shader: {shader * 1000:0.##} ms");
    }
    {
      var (_, _, _, total) = _waterSimulator.TotalSimPerfSampler.GetStats();
      text.Add($"Water simulator total: {total * 1000:0.##} ms");
      var (_, _, _, shader) = _waterSimulator.ShaderPerfSampler.GetStats();
      text.Add($"Water simulator shader: {shader * 1000:0.##} ms");
    }
    var (_, _, _, totalPhysics) = _fixedUpdateSampler.GetStats();
    text.Add($"Total physics cost: {totalPhysics * 1000:0.##} ms");
    return string.Join("\n", text);
  }

  void FixedUpdate() {
    if (!SimulatorEnabled) {
      return;
    }
    
    _stopwatch.Start();

    Self._waterSimulator.TickPipeline();

    PreparePersistentValues();
    Self._contaminationSimulator.TickPipeline();
    Self._moistureSimulator.TickPipeline();
    Self._contaminationSimulator.ProcessOutput();
    Self._moistureSimulator.ProcessOutput();

    _stopwatch.Stop();
    _fixedUpdateSampler.AddSample(_stopwatch.Elapsed.TotalSeconds);
    _stopwatch.Reset();
  }

  void PreparePersistentValues() {
    //FIXME: React on the change events and don't update everything.
    //FIXME: Maybe deprecate packed inout all together? Need AMD testing.
    var packedInput1 = _packedPersistentValuesBuffer1.Values;
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
          Contaminations = _waterContaminationService.Contamination(index),
          WaterDepths = _waterService.WaterDepth(index),
          UnsafeCellHeights = _terrainService.UnsafeCellHeight(index),
          BitmapFlags = bitmapFlags,
      };
    }
    _packedPersistentValuesBuffer1.PushToGpu(null);
  }

  struct InputStruct1 {
    public float Contaminations;
    public float WaterDepths;
    public int UnsafeCellHeights;
    public uint BitmapFlags;

    public const uint ContaminationBarrierBit = 0x0001;
    public const uint AboveMoistureBarrierBit = 0x0002;
    public const uint FullMoistureBarrierBit = 0x0004;
    public const uint WaterTowerIrrigatedBit = 0x0008;
  };

  public BaseBuffer PackedPersistentValuesBuffer1 => _packedPersistentValuesBuffer1;
  SimpleBuffer<InputStruct1> _packedPersistentValuesBuffer1;

  void SetupPersistentValues() {
    var totalMapSize = _mapIndexService.TotalMapSize;
    _packedPersistentValuesBuffer1 = new SimpleBuffer<InputStruct1>(
        "PackedInput1", new InputStruct1[totalMapSize]);
  }

  #region A helper class whose sole role is to deliver FixedUpdate to the singleton.

  sealed class FixedUpdateListener : MonoBehaviour {
    void FixedUpdate() {
      Self.FixedUpdate();
    }
  }

  #endregion

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    SetupPersistentValues();
    new GameObject(GetType().FullName + "#FixedTicker").AddComponent<FixedUpdateListener>();
    _contaminationSimulator.Initialize(this);
    _moistureSimulator.Initialize(this);
    _waterSimulator.Initialize();
  }

  #endregion
}

}
