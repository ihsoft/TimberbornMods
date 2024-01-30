// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberCommons.IrrigationSystem;
using IgorZ.TimberDev.Utils;
using Timberborn.Growing;
using Timberborn.GrowingUI;
using Timberborn.Localization;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.IrrigationSystemUI {

/// Harmony patch to show fractional grow days for growables.
[HarmonyPatch(typeof(GrowableFragment), nameof(GrowableFragment.UpdateFragment))]
static class GrowableFragmentPatch {
  // ReSharper disable once UnusedMember.Local
  static void Postfix(ref bool __runOriginal, ILoc ____loc, Label ____growthTime, Growable ____growable) {
    if (!__runOriginal || ____growable == null) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    ____growthTime.text = HoursShortFormatter.Format(____loc, ____growable.GrowthTimeInDays * 24f);
  }
}

}
