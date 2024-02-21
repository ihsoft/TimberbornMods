// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.SoilMoistureSystem;
using UnityEngine;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.WaterService {

[HarmonyPatch(typeof(SoilMoistureMap), nameof(SoilMoistureMap.UpdateDesertIntensity))]
static class SoilMoistureMapUpdateDesertIntensityPatch {
  // ReSharper disable once UnusedMember.Local
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
