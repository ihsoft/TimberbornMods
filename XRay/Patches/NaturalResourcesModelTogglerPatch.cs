// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.XRay.Core;
using Timberborn.NaturalResourcesUI;
// ReSharper disable UnusedMember.Local

namespace IgorZ.XRay.Patches;

[HarmonyPatch(typeof(NaturalResourcesModelToggler))]
static class NaturalResourcesModelTogglerPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(NaturalResourcesModelToggler.ToggleNaturalResourceModels))]
  static void ToggleNaturalResourceModelsPostfix() {
    NaturalResourceVisibilityService.Instance?.RefreshAfterDebugToggle();
  }
}
