// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using IgorZ.TimberDev.UI;
using Timberborn.Growing;
using Timberborn.Localization;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches {

/// Harmony patch to show grow rate as days/hours.
[HarmonyPatch]
static class GrowableFragmentPatch {
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.GrowingUI.GrowableFragment:UpdateFragment");
  }

  static void Postfix(bool __runOriginal, ILoc ____loc, Label ____growthTime, Growable ____growable) {
    if (!__runOriginal || !____growable) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    ____growthTime.text = CommonFormats.DaysHoursFormat(____loc, ____growable.GrowthTimeInDays * 24f);
  }
}

}
