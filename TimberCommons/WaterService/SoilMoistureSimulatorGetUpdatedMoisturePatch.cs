// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using HarmonyLib;
using Timberborn.SoilMoistureSystem;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.WaterService {

/// <summary>Harmony patch to override moisture levels.</summary>
[HarmonyPatch(typeof(SoilMoistureSimulator), nameof(SoilMoistureSimulator.GetUpdatedMoisture))]
static class SoilMoistureSimulatorGetUpdatedMoisturePatch {
  // It will be accessed from the threads, so don't modify the dict once assigned.
  public static Dictionary<int, float> MoistureOverrides;

  // ReSharper disable once UnusedMember.Local
  static void Postfix(int index, bool __runOriginal, ref float __result) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }

    // Get a reference since the overrides instance can be updated from another thread.
    var overrides = MoistureOverrides;
    if (overrides != null && overrides.TryGetValue(index, out var newLevel)) {
      __result = __result < newLevel ? newLevel : __result;
    }  }
}

}
