﻿// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using HarmonyLib;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.UI;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.GoodConsumingBuildingSystemUI;
using Timberborn.Localization;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.CommonUIPatches {

/// <summary>Harmony patch to show supply in days and hours.</summary>
/// <remarks>
/// It takes the localized string from the stock game and tries to re-use it. If the result is bad, then disable feature
/// "GoodConsumingBuildingUI.DaysHoursViewForAllBuildings" to fail back to the old behavior (only show hours).
/// </remarks>
[HarmonyPatch(typeof(GoodConsumingBuildingFragment), nameof(GoodConsumingBuildingFragment.UpdateProgressBar))]
static class GoodConsumingBuildingFragmentPatch {
  const string NoTilesToIrrigateLocKey = "IgorZ.TimberCommons.WaterTower.NoTilesToIrrigate";

  // ReSharper disable once UnusedMember.Local
  static bool Prefix(bool __runOriginal, ILoc ____loc,
                     Timberborn.CoreUI.ProgressBar ____hoursLeftBar, Label ____hoursLeft,
                     GoodConsumingBuilding ____goodConsumingBuilding) {
    if (!__runOriginal) {
      return false;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (____goodConsumingBuilding.GoodPerHour > float.Epsilon) {
      return true;
    }
    ____hoursLeftBar.SetProgress(0);
    ____hoursLeft.text = ____loc.T(NoTilesToIrrigateLocKey);
    return false;
  }

  // ReSharper disable once UnusedMember.Local
  static void Postfix(bool __runOriginal, ILoc ____loc, Label ____hoursLeft,
                      GoodConsumingBuilding ____goodConsumingBuilding) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (!Features.GoodConsumingBuildingUIDaysHoursForAll) {
      return;
    }
    ____hoursLeft.text = CommonFormats.FormatSupplyLeft(____loc, ____goodConsumingBuilding.HoursUntilNoSupply);
  }
}

}
