// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberDev.UI;
using Timberborn.Growing;
using Timberborn.GrowingUI;
using Timberborn.Localization;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.CommonUI {

/// <summary>Harmony patch to show growth time in days and hours.</summary>
[HarmonyPatch(typeof(GrowableToolPanelItemFactory), nameof(GrowableToolPanelItemFactory.Create))]
static class GrowableToolPanelItemFactoryPatch {
  // ReSharper disable once UnusedMember.Local
  static void Postfix(Growable growable, bool __runOriginal, ref VisualElement __result, ILoc ____loc) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    __result.Q<Label>("GrowthTime").text = CommonFormats.DaysHoursFormat(____loc, growable.GrowthTimeInDays * 24f);
  }
}

}
