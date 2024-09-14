// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using Timberborn.BaseComponentSystem;

namespace IgorZ.TimberCommons.WaterBuildings;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

/// <summary>
/// Redirects requests for "WaterOutput.AvailableSpace" to the custom implementation in
/// <see cref="AdjustableWaterOutput"/>.
/// </summary>
[HarmonyPatch]
static class WaterOutputPatch {
  static MethodBase TargetMethod() {
    return AccessTools.PropertyGetter("Timberborn.WaterBuildings.WaterOutput:AvailableSpace");
  }

  static bool Prefix(bool __runOriginal, BaseComponent __instance, ref float __result) {
    if (!__runOriginal) {
      return false;  // The other patches must follow the same style to properly support the skip logic!
    }

    if (__instance is not AdjustableWaterOutput adjuster) {
      return true;
    }
    __result = adjuster.CalculateAvailableSpace();
    return false;
  }
}
