// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.SoilMoistureSystem;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.WaterService {

/// <summary>Harmony patch to override moisture levels.</summary>
[HarmonyPatch(typeof(SoilMoistureSimulator), nameof(SoilMoistureSimulator.CalculateMoistureForCell))]
static class SoilMoistureSimulatorPatch {
  // ReSharper disable once UnusedMember.Local
  static void Postfix(int index, ref float __result, bool __runOriginal) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (DirectSoilMoistureSystemAccessor.MoistureLevelOverrides.TryGetValue(index, out var newLevel)
        && __result < newLevel) {
      __result = newLevel;
    }
  }
}

}
