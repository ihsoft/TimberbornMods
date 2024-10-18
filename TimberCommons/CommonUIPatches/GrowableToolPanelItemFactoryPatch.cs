// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using IgorZ.TimberCommons.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.Growing;
using Timberborn.Localization;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches {

/// <summary>Harmony patch to show growth time in days and hours.</summary>
[HarmonyPatch]
static class GrowableToolPanelItemFactoryPatch {
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.GrowingUI.GrowableToolPanelItemFactory:Create");
  }

  static void Postfix(GrowableSpec growableSpec, bool __runOriginal, ref VisualElement __result, ILoc ____loc) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (!TimeAndDurationSettings.DaysHoursGrowingTime) {
      return;
    }
    __result.Q<Label>("GrowthTime").text = CommonFormats.DaysHoursFormat(____loc, growableSpec.GrowthTimeInDays * 24f);
  }
}

}
