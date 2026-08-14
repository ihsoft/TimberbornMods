// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using HarmonyLib;
using IgorZ.XRay.Core;
using Timberborn.SelectionSystem;
using UnityEngine;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace IgorZ.XRay.Patches;

[HarmonyPatch(typeof(SelectableObjectRaycaster))]
static class SelectableObjectRaycasterPatch {
  [HarmonyPrefix]
  [HarmonyPatch(
      nameof(SelectableObjectRaycaster.TryHitSelectableObject),
      [typeof(Ray), typeof(bool), typeof(SelectableObject), typeof(RaycastHit)],
      [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out])]
  static bool TryHitSelectableObjectPrefix(
      SelectableObjectRaycaster __instance, Ray worldSpaceRay, bool includeTerrainStump,
      ref SelectableObject hitObject, ref RaycastHit raycastHit, ref bool __result) {
    if (!XRayModeManager.Instance.PassThroughSurfaceObjects) {
      return true;
    }
    var hits = Physics.RaycastAll(worldSpaceRay);
    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
    if (!__instance.HitTerrain(worldSpaceRay, includeTerrainStump, out var terrainDistance)) {
      return true;
    }
    var surfaceObjects = new HashSet<SelectableObject>();
    foreach (var hit in hits) {
      if (!__instance._selectableObjectRetriever.TryGetSelectableObject(hit.collider.gameObject, out var selectable)) {
        continue;
      }
      if (hit.distance <= terrainDistance) {
        surfaceObjects.Add(selectable);
      } else if (!surfaceObjects.Contains(selectable)) {
        hitObject = selectable;
        raycastHit = hit;
        __result = true;
        return false;
      }
    }
    hitObject = null;
    raycastHit = default;
    __result = false;
    return false;
  }

  [HarmonyPrefix]
  [HarmonyPatch(nameof(SelectableObjectRaycaster.HitIsCloserThanTerrain))]
  static bool HitIsCloserThanTerrainPrefix(ref bool __result) {
    if (!XRayModeManager.Instance.IsActive) {
      return true;
    }
    __result = true;
    return false;
  }
}
