// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.GoodConsumingBuildingSystemUI;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.IrrigationSystemUI {

/// Harmony patch to show supply in days and hours.
[HarmonyPatch(typeof(GoodConsumingBuildingFragment), nameof(GoodConsumingBuildingFragment.UpdateProgressBar))]
static class GoodConsumingBuildingFragmentPatch {
  const string SupplyRemainingLocKey = "IgorZ.TimberCommons.WaterTower.SupplyRemaining";
  const float SwitchToDaysThreshold = 24f;

  [SuppressMessage("ReSharper", "InconsistentNaming")]
  // ReSharper disable once UnusedMember.Local
  static void Postfix(ref bool __runOriginal, ILoc ____loc, Label ____hoursLeft,
                      GoodConsumingBuilding ____goodConsumingBuilding) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (____goodConsumingBuilding.HoursUntilNoSupply > SwitchToDaysThreshold) {
      var duration = Mathf.RoundToInt(____goodConsumingBuilding.HoursUntilNoSupply);
      ____hoursLeft.text = ____loc.T(SupplyRemainingLocKey, duration / 24, duration % 24);
    }
  }
}

}
