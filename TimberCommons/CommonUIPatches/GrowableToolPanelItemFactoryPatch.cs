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

/// <summary>Harmony patch to show growth time in days and hours.</summary>
[HarmonyPatch(typeof(GrowableToolPanelItemFactory), nameof(GrowableToolPanelItemFactory.Create))]
static class GrowableToolPanelItemFactoryPatch {
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