// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using HarmonyLib;
using IgorZ.TimberDev.UI;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.GoodConsumingBuildingSystemUI;
using Timberborn.Localization;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.IrrigationSystemUI {

/// Harmony patch to show supply in days and hours.
[HarmonyPatch(typeof(GoodConsumingBuildingFragment), nameof(GoodConsumingBuildingFragment.UpdateProgressBar))]
static class GoodConsumingBuildingFragmentPatch {
  const string SupplyRemainingLocKey = "GoodConsuming.SupplyRemaining";
  static string _localizedSupplyRemainingTmpl;

  // ReSharper disable once UnusedMember.Local
  static void Postfix(bool __runOriginal, ILoc ____loc, Label ____hoursLeft,
                      GoodConsumingBuilding ____goodConsumingBuilding) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (_localizedSupplyRemainingTmpl == null) {
      var original = ____loc.T(SupplyRemainingLocKey, "###");
      _localizedSupplyRemainingTmpl = original.Substring(0, original.IndexOf("###", StringComparison.Ordinal)) + "{0}";
    }
    ____hoursLeft.text = string.Format(
        _localizedSupplyRemainingTmpl,
        CommonFormats.DaysHoursFormat(____loc, ____goodConsumingBuilding.HoursUntilNoSupply));
  }
}

}
