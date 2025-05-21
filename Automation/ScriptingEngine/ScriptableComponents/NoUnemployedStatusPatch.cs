// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Timberborn.StatusSystem;
using Timberborn.WorkSystem;
using Timberborn.WorkSystemUI;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

[HarmonyPatch(typeof(NoUnemployedStatus), nameof(NoUnemployedStatus.UpdateStatus))]
static class NoUnemployedStatusPatch {
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  static bool Prefix(bool __runOriginal, Workplace ____workplace, StatusToggle ____statusToggle) {
    if (!__runOriginal) {
      return true;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (____workplace.DesiredWorkers != 0) {
      return true;
    }
    ____statusToggle.Deactivate();
    return false;  // No need to check for unemployed workers if the workplace is assumed to be empty.
  }
}
