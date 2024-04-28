// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics;
using Timberborn.SingletonSystem;
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
  readonly Stopwatch _stopwatch = new();
  readonly ValueSampler _fixedUpdateSampler = new(10);

  internal static GpuSimulatorsController Self;
  internal bool ContaminationSimulatorEnabled => _contaminationSimulator.IsEnabled;
  internal bool MoistureSimulatorEnabled => _moistureSimulator.IsEnabled;
  internal bool WaterSimulatorEnabled => _waterSimulator.IsEnabled;

  GpuSimulatorsController(
      GpuSoilContaminationSimulator contaminationSimulator,
      GpuSoilMoistureSimulator moistureSimulator,
      GpuWaterSimulator waterSimulator) {
    _contaminationSimulator = contaminationSimulator;
    _moistureSimulator = moistureSimulator;
    _waterSimulator = waterSimulator;
    Self = this;
  }

  internal void EnableSoilContaminationSim(bool state) {
    _contaminationSimulator.IsEnabled = state;
  }

  internal void EnableSoilMoistureSim(bool state) {
    _moistureSimulator.IsEnabled = state;
  }

  internal void EnableWaterSimulator(bool state) {
    _waterSimulator.IsEnabled = state;
  }

  internal string GetStatsText() {
    var text = new List<string>(15);
    if (_contaminationSimulator.IsEnabled) {
      var (_, _, _, total) = _contaminationSimulator.GetTotalStats();
      text.Add($"Soil contamination total: {total * 1000:0.##} ms");
      var (_, _, _, shader) = _contaminationSimulator.GetShaderStats();
      text.Add($"Soil contamination shader: {shader * 1000:0.##} ms");
    } else {
      text.Add("Soil contamination simulation disabled");
    }
    if (_moistureSimulator.IsEnabled) {
      var (_, _, _, total) = _moistureSimulator.GetTotalStats();
      text.Add($"Soil moisture total: {total * 1000:0.##} ms");
      var (_, _, _, shader) = _moistureSimulator.GetShaderStats();
      text.Add($"Soil moisture shader: {shader * 1000:0.##} ms");
    } else {
      text.Add("Soil moisture simulation disabled");
    }
    if (_waterSimulator.IsEnabled) {
      var (_, _, _, total) = _waterSimulator.GetTotalStats();
      text.Add($"Water simulator total: {total * 1000:0.##} ms");
      var (_, _, _, shader) = _waterSimulator.GetShaderStats();
      text.Add($"Water simulator shader: {shader * 1000:0.##} ms");
    } else {
      text.Add("Water simulation disabled");
    }
    var (_, _, _, totalPhysics) = _fixedUpdateSampler.GetStats();
    text.Add($"Total physics cost: {totalPhysics * 1000:0.##} ms");
    return string.Join("\n", text);
  }

  void FixedUpdate() {
    _stopwatch.Start();
    if (Self._contaminationSimulator.IsEnabled){
      Self._contaminationSimulator.TickPipeline();
    }
    if (Self._moistureSimulator.IsEnabled) {
      Self._moistureSimulator.TickPipeline();
    }
    if (Self._waterSimulator.IsEnabled) {
      Self._waterSimulator.TickPipeline();
    }
    _stopwatch.Stop();
    _fixedUpdateSampler.AddSample(_stopwatch.Elapsed.TotalSeconds);
    _stopwatch.Reset();
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
    new GameObject(GetType().FullName + "#FixedTicker").AddComponent<FixedUpdateListener>();
    _contaminationSimulator.Initialize();
    _moistureSimulator.Initialize();
    _waterSimulator.Initialize();
  }

  #endregion
}

}
