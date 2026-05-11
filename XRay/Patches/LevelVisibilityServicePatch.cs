// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.LevelVisibilitySystem;

namespace IgorZ.XRay.Patches;

[HarmonyPatch(typeof(LevelVisibilityService), nameof(LevelVisibilityService.MaxVisibleLevel), MethodType.Getter)]
static class LevelVisibilityServicePatch {
  internal static bool XrayModeEnabled;

  // ReSharper disable once UnusedMember.Local
  // ReSharper disable once InconsistentNaming
  static void Postfix(ref int __result) {
    if (XrayModeEnabled) {
      __result = 0;
    }
  }
}

// FIXME: Be smart! Allow buidling underground.
// Timberborn.LevelVisibilitySystem.ILevelVisibilityService
// Timberborn.BlockSystem.StackableBlockService.IsVisibleStackableAt
// Timberborn.BlockObjectPickingSystem.BlockObjectPreviewPicker.IsStackable