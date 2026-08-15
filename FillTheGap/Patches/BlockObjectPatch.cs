// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.FillTheGap.Core;
using Timberborn.BlockSystem;

namespace IgorZ.FillTheGap.Patches;

[HarmonyPatch(typeof(BlockObject))]
static class BlockObjectPatch {
  static PlatformReplacementService _platformReplacementService;

  internal static void SetService(PlatformReplacementService platformReplacementService) {
    _platformReplacementService = platformReplacementService;
  }

  [HarmonyPatch(nameof(BlockObject.CanDelete))]
  [HarmonyPostfix]
  static void CanDeletePostfix(BlockObject __instance, ref bool __result) {
    if (__result || _platformReplacementService == null) {
      return;
    }

    if (__instance.HasComponent<FillTheGapSpec>()
        && _platformReplacementService.CanDeleteIndependently(__instance)) {
      __result = true;
    }
  }
}
