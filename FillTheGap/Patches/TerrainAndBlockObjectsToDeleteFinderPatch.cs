// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.FillTheGap.Core;
using Timberborn.BlockSystem;
using Timberborn.TerrainPhysics;

namespace IgorZ.FillTheGap.Patches;

[HarmonyPatch(typeof(TerrainAndBlockObjectsToDeleteFinder))]
static class TerrainAndBlockObjectsToDeleteFinderPatch {
  static PlatformReplacementService _platformReplacementService;

  internal static void SetService(PlatformReplacementService platformReplacementService) {
    _platformReplacementService = platformReplacementService;
  }

  [HarmonyPatch(nameof(TerrainAndBlockObjectsToDeleteFinder.AddNextBlockObjectToValidate))]
  [HarmonyPrefix]
  static bool AddNextBlockObjectToValidatePrefix(BlockObject blockObject) {
    if (_platformReplacementService == null || !blockObject.HasComponent<FillTheGapSpec>()) {
      return true;
    }

    return _platformReplacementService.ShouldPropagateDeletionAbove(blockObject);
  }
}
