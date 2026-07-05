// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.XRay.Core;
using Timberborn.TerrainSystemRendering;
using UnityEngine;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace IgorZ.XRay.Patches;

[HarmonyPatch(typeof(TerrainMeshManager))]
static class TerrainMeshManagerPatch {
  [HarmonyPrefix]
  [HarmonyPatch(nameof(TerrainMeshManager.CollectMeshesForCoordinates))]
  static bool CollectMeshesForCoordinatesPrefix(Vector3Int coordinates) {
    var service = TransparentTerrainMeshService.Instance;
    return service == null || service.ShouldRenderTerrainCoordinate(coordinates);
  }
}
