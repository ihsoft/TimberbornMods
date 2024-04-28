// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberCommons.GpuSimulators;
using Timberborn.WaterSystem;

namespace IgorZ.TimberCommons.MultiThreadSimulators {

/// <summary>Intercepts stock game simulation thread to run a custom simulator.</summary>
[HarmonyPatch(typeof(WaterSimulator), nameof(WaterSimulator.ProcessSimulation))]
sealed class ParallelWaterSimulatorPatch {
  static ParallelWaterSimulator _patchedSimulator;
  internal static bool UsePatchedSimulator;

  internal static void Initialize() {
    _patchedSimulator = null;
  }

  // ReSharper disable once UnusedMember.Local
  // ReSharper disable once InconsistentNaming
  static bool Prefix(WaterSimulator __instance) {
    if (!UsePatchedSimulator || GpuSimulatorsController.Self.WaterSimulatorEnabled) {
      return true;
    }
    _patchedSimulator ??= new ParallelWaterSimulator(__instance);
    _patchedSimulator.ProcessSimulation();
    return false;
  }
}

}
