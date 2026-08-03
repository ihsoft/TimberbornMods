// Timberborn Mod: Dual District Storage
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;

namespace IgorZ.DualDistrictStorage;

[HarmonyPatch(typeof(BlockObjectToolFinder), nameof(BlockObjectToolFinder.TryFindTool))]
static class BlockObjectToolFinderPatch {
  static bool Prefix(BaseComponent entity, ref Action toolActivationAction, ref bool __result) {
    if (!IsHiddenDualDistrictStorageTemplate(entity)) {
      return true;
    }

    toolActivationAction = null;
    __result = false;
    return false;
  }

  static bool IsHiddenDualDistrictStorageTemplate(BaseComponent entity) {
    if (!entity.HasComponent<DualDistrictStorageSpec>()) {
      return false;
    }

    var placeableBlockObjectSpec = entity.GetComponent<PlaceableBlockObjectSpec>();
    return placeableBlockObjectSpec.DevModeTool && string.IsNullOrEmpty(placeableBlockObjectSpec.ToolGroupId);
  }
}
