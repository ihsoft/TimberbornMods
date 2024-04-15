// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.WaterSystem;

namespace IgorZ.TimberCommons.WaterService {

[HarmonyPatch(typeof(WaterSimulator), nameof(WaterSimulator.ProcessSimulation))]
public class ParallelWaterSimulatorPatch {
  static ParallelWaterSimulator PatchedWaterSimulator;

  internal static void Initialize() {
    PatchedWaterSimulator = null;
  }

  // ReSharper disable once UnusedMember.Local
  // ReSharper disable once InconsistentNaming
  static bool Prefix(WaterSimulator __instance) {
    if (WaterSourceFragmentDebug.UsePatchedSimulation) {
      PatchedWaterSimulator ??= new ParallelWaterSimulator(__instance);
      PatchedWaterSimulator.ProcessSimulation();
      return false;
    }
    return true;
  }
}

}
