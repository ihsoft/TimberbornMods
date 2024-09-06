// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using IgorZ.TimberDev.UI;
using Timberborn.Goods;
using Timberborn.Localization;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches {

/// <summary>Harmony patch to show days and hours for the slow recipes.</summary>
[HarmonyPatch]
static class ManufactoryDescriberPatch1 {
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.WorkshopsUI.ManufactoryDescriber:GetCraftingTime");
  }

  static bool Prefix(RecipeSpecification productionRecipe, float workers,
                     ILoc ____loc, bool __runOriginal, ref string __result) {
    if (!__runOriginal) {
      return false; // The other patches must follow the same style to properly support the skip logic!
    }
    var duration = productionRecipe.CycleDurationInHours / workers;
    __result = CommonFormats.DaysHoursFormat(____loc, duration);
    return false;
  }
}

}
