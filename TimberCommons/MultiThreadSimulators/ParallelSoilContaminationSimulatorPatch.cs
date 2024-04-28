// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberCommons.GpuSimulators;
using Timberborn.SoilContaminationSystem;

namespace IgorZ.TimberCommons.MultiThreadSimulators {

/// <summary>Intercepts stock game simulation thread to run a custom simulator.</summary>
[HarmonyPatch(typeof(SoilContaminationSimulator), nameof(SoilContaminationSimulator.TickSimulation))]
sealed class ParallelSoilContaminationSimulatorPatch {
  static ParallelSoilContaminationSimulator _patchedSimulator;
  internal static bool UsePatchedSimulator;

  internal static void Initialize() {
    _patchedSimulator = null;
  }

  // ReSharper disable once UnusedMember.Local
  // ReSharper disable once InconsistentNaming
  static bool Prefix(SoilContaminationSimulator __instance) {
    if (!UsePatchedSimulator || GpuSimulatorsController.Self.ContaminationSimulatorEnabled) {
      return true;
    }
    _patchedSimulator ??= new ParallelSoilContaminationSimulator(__instance);
    _patchedSimulator.TickSimulation();
    return false;
  }
}

}
