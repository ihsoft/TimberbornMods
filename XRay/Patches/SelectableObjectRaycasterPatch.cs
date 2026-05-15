// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.XRay.Core;
using Timberborn.SelectionSystem;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace IgorZ.XRay.Patches;

[HarmonyPatch(typeof(SelectableObjectRaycaster))]
static class SelectableObjectRaycasterPatch {
  [HarmonyPrefix]
  [HarmonyPatch(nameof(SelectableObjectRaycaster.HitIsCloserThanTerrain))]
  static bool HitIsCloserThanTerrainPrefix(ref bool __result) {
    if (!XRayService.Instance.IsActive) {
      return true;
    }
    __result = true;
    return false;
  }
}
