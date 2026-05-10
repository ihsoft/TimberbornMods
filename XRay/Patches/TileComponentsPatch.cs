// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using HarmonyLib;
using Timberborn.TerrainSystemRendering;
using UnityEngine;

namespace IgorZ.XRay.Patches;

[HarmonyPatch(typeof(TerrainMeshManager.TileComponents), nameof(TerrainMeshManager.TileComponents.UpdateMesh))]
static class TileComponentsPatch {
  internal static Action<Renderer> FixRenderer;

  // ReSharper disable once UnusedMember.Local
  // ReSharper disable once InconsistentNaming
  static void Postfix(TerrainMeshManager.TileComponents __instance) {
    FixRenderer?.Invoke(__instance._meshRenderer);
  }
}
