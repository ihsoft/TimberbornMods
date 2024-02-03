// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.Utils;
using Timberborn.Growing;
using Timberborn.GrowingUI;
using Timberborn.Localization;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.IrrigationSystemUI {

[HarmonyPatch(typeof(GrowableToolPanelItemFactory), nameof(GrowableToolPanelItemFactory.Create))]
static class GrowableToolPanelItemFactoryPatch {
  // ReSharper disable once UnusedMember.Local
  static void Postfix(Growable growable, ref bool __runOriginal, ref VisualElement __result, ILoc ____loc) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    __result.Q<Label>("GrowthTime").text = HoursShortFormatter.Format(____loc, growable.GrowthTimeInDays * 24f); 
  }
}

}
