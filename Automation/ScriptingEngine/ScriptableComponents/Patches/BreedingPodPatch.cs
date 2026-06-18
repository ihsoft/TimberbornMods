// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using HarmonyLib;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Reproduction;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Patches;

[HarmonyPatch(typeof(BreedingPod))]
static class BreedingPodPatch {
  [HarmonyPrefix]
  [HarmonyPatch(nameof(BreedingPod.FinishGrowthCycle))]
  static void FinishGrowthCyclePrefix(BreedingPod __instance, int ____cyclesRemaining, out bool __state) {
    __state = ____cyclesRemaining == 1;
    if (__state) {
      CompletingPods.Add(__instance);
    }
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(BreedingPod.FinishGrowthCycle))]
  static void FinishGrowthCyclePostfix(bool __runOriginal, BreedingPod __instance, bool __state) {
    var completedGrowth = CompletingPods.Remove(__instance);
    if (!__runOriginal || !__state || !completedGrowth) {
      return;
    }
    var behavior = __instance.GetComponent<AutomationBehavior>();
    if (behavior.TryGetDynamicComponent<BreedingPodScriptableComponent.BreedingPodProgressTracker>(out var tracker)) {
      tracker.OnGrowthCycleFinished();
    }
  }

  static bool IsCompleting(BreedingPod breedingPod) {
    return CompletingPods.Contains(breedingPod);
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(BreedingPod.OnInventoryChanged))]
  static void OnInventoryChangedPostfix(bool __runOriginal, BreedingPod __instance) {
    if (!__runOriginal || IsCompleting(__instance)) {
      return;
    }
    var behavior = __instance.GetComponent<AutomationBehavior>();
    if (behavior.TryGetDynamicComponent<BreedingPodScriptableComponent.BreedingPodProgressTracker>(out var tracker)) {
      tracker.OnInventoryChanged();
    }
  }

  static readonly HashSet<BreedingPod> CompletingPods = [];
}
