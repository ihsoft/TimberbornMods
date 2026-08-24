// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Automation;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Patches;

[HarmonyPatch(typeof(Automator))]
static class AutomatorPatch {
  [HarmonyPatch(nameof(Automator.SetState))]
  [HarmonyPrefix]
  static void SetStatePrefix(AutomatorState ____state, out AutomatorState __state) {
    __state = ____state;
  }

  [HarmonyPatch(nameof(Automator.SetState))]
  [HarmonyPostfix]
  static void SetStatePostfix(bool __runOriginal, Automator __instance, AutomatorState __state) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (__instance._state == __state || !__instance.IsTransmitter) {
      return;
    }
    NotifyStateChanged(__instance);
  }

  [HarmonyPatch(nameof(Automator.OnEnterFinishedState))]
  [HarmonyPostfix]
  static void OnEnterFinishedStatePostfix(bool __runOriginal, Automator __instance) {
    if (__runOriginal && __instance.IsTransmitter && __instance._state == AutomatorState.On) {
      NotifyStateChanged(__instance);
    }
  }

  [HarmonyPatch(nameof(Automator.OnExitFinishedState))]
  [HarmonyPostfix]
  static void OnExitFinishedStatePostfix(bool __runOriginal, Automator __instance) {
    if (__runOriginal && __instance.IsTransmitter && __instance._state == AutomatorState.On) {
      NotifyStateChanged(__instance);
    }
  }

  static void NotifyStateChanged(Automator automator) {
    AutomatorSignalReferenceManager.Instance?.OnStateChanged(automator);

    var behavior = automator.GetComponent<AutomationBehavior>();
    if (!behavior) {
      return;
    }
    if (behavior.TryGetDynamicComponent<AutomatorScriptableComponent.AutomatorStateTracker>(out var tracker)) {
      tracker.OnStateChanged();
    }
  }
}
