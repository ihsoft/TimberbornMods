// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberDev.Utils;
using Timberborn.AutomationUI;

// ReSharper disable UnusedMember.Local

namespace IgorZ.Automation.AutomationSystemUI;

[HarmonyPatch(typeof(TransmitterSelector))]
static class TransmitterSelectorPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(TransmitterSelector.Initialize))]
  static void InitializePostfix(TransmitterSelector __instance) {
    ConflictGuardService.InitializeSelector(__instance);
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(TransmitterSelector.Show))]
  static void ShowPostfix(TransmitterSelector __instance) {
    ConflictGuardService.UpdateSelectorState(__instance);
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(TransmitterSelector.UpdateStateIcon))]
  static void UpdateStateIconPostfix(TransmitterSelector __instance) {
    ConflictGuardService.UpdateSelectorState(__instance);
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(TransmitterSelector.UpdateSelectedValue))]
  static void UpdateSelectedValuePostfix(TransmitterSelector __instance) {
    ConflictGuardService.UpdateSelectorState(__instance);
  }

  static GameAutomationConflictGuardService ConflictGuardService {
    get {
      return StaticBindings.DependencyContainer.GetInstance<GameAutomationConflictGuardService>();
    }
  }
}
