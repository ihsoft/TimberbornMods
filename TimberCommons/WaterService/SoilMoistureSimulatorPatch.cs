// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.SoilMoistureSystem;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.WaterService;

/// <summary>Harmony patch to override moisture levels.</summary>
[HarmonyPatch(typeof(MoistureCalculationTask), nameof(MoistureCalculationTask.CalculateMoistureForCell))]
static class SoilMoistureSimulatorPatch {
  static void Postfix(int index3D, ref float __result, bool __runOriginal) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (SoilOverridesService.MoistureLevelOverrides.TryGetValue(index3D, out var newLevel)
        && __result < newLevel) {
      __result = newLevel;
    }
  }
}