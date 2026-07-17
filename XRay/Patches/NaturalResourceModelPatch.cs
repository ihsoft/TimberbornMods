// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.XRay.Core;
using Timberborn.NaturalResourcesModelSystem;
// ReSharper disable UnusedMember.Local

namespace IgorZ.XRay.Patches;

[HarmonyPatch(typeof(NaturalResourceModel))]
static class NaturalResourceModelPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(NaturalResourceModel.ShowCurrentModel))]
  static void ShowCurrentModelPostfix(NaturalResourceModel __instance) {
    NaturalResourceVisibilityService.Instance?.RefreshModel(__instance);
  }
}
