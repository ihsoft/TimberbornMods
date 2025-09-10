// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberCommons.Settings;
using Timberborn.SoilMoistureSystem;
using UnityEngine;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.WaterService;

[HarmonyPatch(typeof(SoilMoistureService), nameof(SoilMoistureService.UpdateDesertIntensity))]
static class SoilMoistureServicePatch {
  static void Prefix(Vector3Int coordinates, ref float moistureLevel, bool __runOriginal) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (!IrrigationSystemSettings.OverrideDesertLevelsForWaterTowers) {
      return;
    }
    if (SoilOverridesService.TerrainTextureLevelsOverrides.TryGetValue(coordinates, out var newLevel)) {
      moistureLevel = Mathf.Max(moistureLevel, newLevel);
    }
  }
}