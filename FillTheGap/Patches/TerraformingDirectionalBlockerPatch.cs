// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.FillTheGap.Core;
using Timberborn.BlockSystem;
using Timberborn.Terraforming;
using UnityEngine;

namespace IgorZ.FillTheGap.Patches;

[HarmonyPatch(typeof(TerraformingDirectionalBlocker))]
static class TerraformingDirectionalBlockerPatch {
  static PlatformReplacementService _platformReplacementService;

  internal static void SetService(PlatformReplacementService platformReplacementService) {
    _platformReplacementService = platformReplacementService;
  }

  [HarmonyPatch(nameof(TerraformingDirectionalBlocker.IsValidBlockerBlockObject))]
  [HarmonyPostfix]
  static void IsValidBlockerBlockObjectPostfix(
      BlockObject blockObject, Vector3Int coordinates, ref bool __result) {
    if (!__result || _platformReplacementService == null) {
      return;
    }

    if (blockObject.HasComponent<FillTheGapSpec>()
        && _platformReplacementService.HasCompletedPlatformSurfaceAbove(coordinates)) {
      __result = false;
    }
  }

  [HarmonyPatch(nameof(TerraformingDirectionalBlocker.Block))]
  [HarmonyPrefix]
  static bool BlockPrefix(TerraformingDirectionalBlocker other, Vector3Int axis) {
    if (_platformReplacementService == null || axis != Vector3Int.back) {
      return true;
    }

    var lowerBlockObject = other.GetComponent<BlockObject>();
    return !lowerBlockObject.HasComponent<FillTheGapSpec>()
        || !_platformReplacementService.HasCompletedPlatformSurfaceAbove(lowerBlockObject.Coordinates);
  }
}
