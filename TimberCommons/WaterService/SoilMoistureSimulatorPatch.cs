// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.WaterService;

/// <summary>Harmony patch to override moisture levels.</summary>
[HarmonyPatch]
static class SoilMoistureSimulatorPatch {
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.SoilMoistureSystem.SoilMoistureSimulator:CalculateMoistureForCell");
  }

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