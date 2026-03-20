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

[HarmonyPatch(typeof(Automator), nameof(Automator.SetState))]
static class AutomatorPatch {
  static void Prefix(AutomatorState ____state, out AutomatorState __state) {
    __state = ____state;
  }

  static void Postfix(bool __runOriginal, Automator __instance, AutomatorState __state) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (__instance._state == __state || !__instance.IsTransmitter) {
      return;
    }
    var behavior = __instance.GetComponent<AutomationBehavior>();
    if (!behavior) {
      return;
    }
    if (behavior.TryGetDynamicComponent<AutomatorScriptableComponent.AutomatorStateTracker>(out var tracker)) {
      tracker.OnStateChanged();
    }
  }
}
