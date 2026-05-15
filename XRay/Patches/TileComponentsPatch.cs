// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.XRay.Core;
using Timberborn.TerrainSystemRendering;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace IgorZ.XRay.Patches;

[HarmonyPatch(typeof(TerrainMeshManager.TileComponents))]
static class TileComponentsPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(TerrainMeshManager.TileComponents.UpdateMesh))]
  static void UpdateMeshPostfix(TerrainMeshManager.TileComponents __instance) {
    var renderer = __instance._meshRenderer;
    if (renderer && XRayService.Instance.IsActive) {
      XRayService.Instance.SetXRayRenderer(renderer);
    }
  }
}