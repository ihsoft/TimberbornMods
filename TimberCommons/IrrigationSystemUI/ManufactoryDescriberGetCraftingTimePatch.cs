// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberDev.UI;
using Timberborn.Goods;
using Timberborn.Localization;
using Timberborn.WorkshopsUI;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.IrrigationSystemUI {

/// <summary>Shows days/hours format for the recipe duration greater than 24 hours.</summary>
[HarmonyPatch(typeof(ManufactoryDescriber), nameof(ManufactoryDescriber.GetCraftingTime))]
static class ManufactoryDescriberGetCraftingTimePatch {
  // ReSharper disable once UnusedMember.Local
  static bool Prefix(RecipeSpecification productionRecipe, float workers,
                     ILoc ____loc, ref bool __runOriginal, ref string __result) {
    if (!__runOriginal) {
      return false; // The other patches must follow the same style to properly support the skip logic!
    }
    var duration = productionRecipe.CycleDurationInHours / workers;
    __result = CommonFormats.DaysHoursFormat(____loc, duration);
    return false;
  }
}

}
