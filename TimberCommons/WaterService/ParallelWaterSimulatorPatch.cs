// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.WaterSystem;

namespace IgorZ.TimberCommons.WaterService {

/// <summary>Intercepts stock game simulation thread to run a custom simulator.</summary>
[HarmonyPatch(typeof(WaterSimulator), nameof(WaterSimulator.ProcessSimulation))]
sealed class ParallelWaterSimulatorPatch {
  static ParallelWaterSimulator _patchedWaterSimulator;
  internal static bool UsePatchedSimulator;

  internal static void Initialize() {
    _patchedWaterSimulator = null;
  }

  // ReSharper disable once UnusedMember.Local
  // ReSharper disable once InconsistentNaming
  static bool Prefix(WaterSimulator __instance) {
    if (!UsePatchedSimulator) {
      return true;
    }
    _patchedWaterSimulator ??= new ParallelWaterSimulator(__instance);
    _patchedWaterSimulator.ProcessSimulation();
    return false;
  }
}

}
