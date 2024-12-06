// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

/// <summary>Delivers height change events into the scriptable component.</summary>
[HarmonyPatch(typeof(Floodgate), nameof(Floodgate.Height), MethodType.Setter)]
static class FloodgateSetHeightPatch {
  // ReSharper disable once UnusedMember.Local
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  static void Postfix(bool __runOriginal, BaseComponent __instance, float value) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    var scriptableComponent = __instance.GetComponentFast<FloodgateScriptableComponent>();
    if (scriptableComponent) {
      scriptableComponent.OnSetHeight(value);
    }
  }
}
