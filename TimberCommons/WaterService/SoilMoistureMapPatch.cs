// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using UnityEngine;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.WaterService {

[HarmonyPatch]
static class SoilMoistureMapPatch {
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.SoilMoistureSystem.SoilMoistureMap:UpdateDesertIntensity");
  }

  static void Prefix(Vector2Int coordinates, ref float moistureLevel, bool __runOriginal) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (DirectSoilMoistureSystemAccessor.TerrainTextureLevelsOverrides.TryGetValue(coordinates, out var newLevel)) {
      moistureLevel = Mathf.Max(moistureLevel, newLevel);
    }
  }
}

}
