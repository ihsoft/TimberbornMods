// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberCommons.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.Localization;
using Timberborn.Workshops;
using Timberborn.WorkshopsUI;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches;

/// <summary>Harmony patch to show days and hours for the slow recipes.</summary>
[HarmonyPatch(typeof(ManufactoryDescriber), nameof(ManufactoryDescriber.GetCraftingTime))]
static class ManufactoryDescriberPatch1 {
  static bool Prefix(RecipeSpec productionRecipe, float workers,
                     ILoc ____loc, bool __runOriginal, ref string __result) {
    if (!__runOriginal) {
      return false; // The other patches must follow the same style to properly support the skip logic!
    }
    if (!TimeAndDurationSettings.DaysHoursForRecipeDuration) {
      return true;
    }
    var duration = productionRecipe.CycleDurationInHours / workers;
    __result = CommonFormats.DaysHoursFormat(____loc, duration);
    return false;
  }
}