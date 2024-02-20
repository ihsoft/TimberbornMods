// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.TerrainSystem;
using UnityEngine;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.WaterService {

/// <summary>Harmony patch that changes desert appearance of the irrigated tiles based on the overrides.</summary>
[HarmonyPatch(typeof(TerrainMaterialMap), nameof(TerrainMaterialMap.SetDesertIntensity))]
static class TerrainMaterialMapSetDesertIntensityPatch {
  // ReSharper disable once UnusedMember.Local
  static void Prefix(Vector2Int coordinates, ref float desertIntensity, bool __runOriginal) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }

    var overrides = DirectSoilMoistureSystemAccessor.DesertLevelOverrides;
    if (overrides == null || !overrides.TryGetValue(coordinates, out var newLevel) || newLevel > desertIntensity) {
      return;
    }
    desertIntensity = newLevel;
  }
}

}
