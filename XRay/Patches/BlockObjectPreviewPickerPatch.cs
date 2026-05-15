// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.XRay.Core;
using Timberborn.BlockObjectPickingSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using UnityEngine;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace IgorZ.XRay.Patches;

[HarmonyPatch(typeof(BlockObjectPreviewPicker))]
static class BlockObjectPreviewPickerPatch {
  [HarmonyPrefix]
  [HarmonyPatch(nameof(BlockObjectPreviewPicker.CenteredPreviewCoordinates))]
  static bool CenteredPreviewCoordinatesPrefix(
      PlaceableBlockObjectSpec placeableBlockObjectSpec, Orientation orientation, Ray ray,
      ref PickedCoordinates? __result) {
    if (!XRayService.Instance.IsActive) {
      return true;
    }
    __result = TerrainRayCaster.Instance.CenteredPreviewCoordinates(placeableBlockObjectSpec, orientation, ray);
    return false;
  }
}
