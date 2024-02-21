// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.SoilMoistureSystem;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.WaterService {

/// <summary>Harmony patch to override moisture levels.</summary>
[HarmonyPatch(typeof(SoilMoistureSimulator), nameof(SoilMoistureSimulator.GetUpdatedMoisture))]
static class SoilMoistureSimulatorGetUpdatedMoisturePatch {
  // ReSharper disable once UnusedMember.Local
  static void Postfix(int index, bool __runOriginal, ref float __result) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (DirectSoilMoistureSystemAccessor.MoistureLevelOverrides.TryGetValue(index, out var newLevel)) {
      __result = __result < newLevel ? newLevel : __result;
    }
  }
}

}
