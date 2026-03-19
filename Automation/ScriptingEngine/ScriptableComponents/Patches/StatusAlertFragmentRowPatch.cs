// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HarmonyLib;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.StatusSystemUI;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Patches;

/// <summary>
/// Removes prefix from the alert text. This prefix can be added in <see cref="NotificationsScriptableComponent"/> to
/// disambiguate alerts with the same text, but different icons.
/// </summary>
[HarmonyPatch(typeof(StatusAlertFragmentRow), nameof(StatusAlertFragmentRow.GetAlertText))]
static class StatusAlertFragmentRowPatch {
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  static string Postfix(string __result, bool __runOriginal) {
    if (!__runOriginal) {
      return __result;  // The other patches must follow the same style to properly support the skip logic!
    }
    return __result.Split("###").Last();
  }
}
