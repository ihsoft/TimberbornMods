// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberCommons.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.Growing;
using Timberborn.GrowingUI;
using Timberborn.Localization;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches;

/// Harmony patch to show grow rate as days/hours.
[HarmonyPatch(typeof(GrowableFragment), nameof(GrowableFragment.UpdateFragment))]
static class GrowableFragmentPatch {
  static void Postfix(bool __runOriginal, ILoc ____loc, Label ____growthTime, Growable ____growable) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (!____growable || !TimeAndDurationSettings.DaysHoursGrowingTime) {
      return;
    }
    ____growthTime.text = CommonFormats.DaysHoursFormat(____loc, ____growable.GrowthTimeInDays * 24f);
  }
}