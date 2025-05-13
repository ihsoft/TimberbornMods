// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

[HarmonyPatch(typeof(Floodgate), nameof(Floodgate.Height), MethodType.Setter)]
static class FloodgatePatch {
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  static void Postfix(bool __runOriginal, Floodgate __instance, float value) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    var automationTracker = __instance.GetComponentFast<FloodgateScriptableComponent.HeightChangeTracker>();
    if (automationTracker) {
      automationTracker.OnHeighChanged();
    }
  }
}
