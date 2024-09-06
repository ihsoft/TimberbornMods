// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using IgorZ.TimberDev.UI;
using Timberborn.Goods;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches {

/// <summary>Harmony patch to show low fuel ingredient consumption rate in the recipe UIs.</summary>
[HarmonyPatch]
static class ManufactoryDescriberPatch2 {
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.WorkshopsUI.ManufactoryDescriber:GetInputs");
  }

  static void Postfix(RecipeSpecification productionRecipe, bool __runOriginal,
                      ref IEnumerable<VisualElement> __result) {
    if (!__runOriginal) {
      return; // The other patches must follow the same style to properly support the skip logic!
    }
    if (!productionRecipe.ConsumesFuel) {
      return;
    }
    var inputs = __result.ToList();
    var fuelAmountLabel = inputs.Last().Q<Label>("Amount");
    if (fuelAmountLabel != null) {
      fuelAmountLabel.text = CommonFormats.FormatSmallValue(1f / productionRecipe.CyclesFuelLasts);
    } else {
      DebugEx.Warning("Cannot override fuel consumption rate for recipe: {0}", productionRecipe.Id);
    }
    __result = inputs;
  }
}

}
