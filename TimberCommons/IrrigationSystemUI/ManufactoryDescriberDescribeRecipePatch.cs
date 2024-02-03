// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using IgorZ.TimberDev.Utils;
using Timberborn.EntityPanelSystem;
using Timberborn.Goods;
using Timberborn.GoodsUI;
using Timberborn.WorkshopsUI;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.IrrigationSystemUI {

/// <summary>Deals with too low fuel ingredient consumption rate in the recipe tooltips.</summary>
[HarmonyPatch(typeof(ManufactoryDescriber), nameof(ManufactoryDescriber.DescribeRecipe),
              typeof(RecipeSpecification), typeof(float))]
static class ManufactoryDescriberDescribeRecipePatch1 {
  static bool Prefix(RecipeSpecification productionRecipe, float workers,
                     ref bool __runOriginal, ref VisualElement __result, ManufactoryDescriber __instance,
                     ProductionItemFactory ____productionItemFactory, GoodDescriber ____goodDescriber) {
    if (!__runOriginal) {
      return false; // The other patches must follow the same style to properly support the skip logic!
    }
    if (!productionRecipe.ConsumesFuel) {
      return true;
    }
    __result = ____productionItemFactory.CreateInputOutput(
        GetAdjustedInputs(productionRecipe, __instance, ____goodDescriber),
        __instance.GetOutputs(productionRecipe),
        __instance.GetCraftingTime(productionRecipe, workers));
    return false;
  }

  internal static IEnumerable<VisualElement> GetAdjustedInputs(
      RecipeSpecification productionRecipe, ManufactoryDescriber instance, GoodDescriber goodDescriber) {
    var amount = 1f / productionRecipe.CyclesFuelLasts;
    var describedGood = goodDescriber.GetDescribedGood(productionRecipe.Fuel.Id);
    var tooltip = ManufactoryDescriber.GetTooltip(describedGood);
    var newElement = instance.CreateElement(describedGood, FloatValueFormatter.FormatSmallValue(amount), tooltip);
    var inputs = instance.GetInputs(productionRecipe).ToArray();
    inputs[inputs.Length - 1] = newElement;
    return inputs;
  }
}

/// <summary>Deals with too low fuel ingredient consumption rate in the manufactory UI fragment.</summary>
[HarmonyPatch(typeof(ManufactoryDescriber), nameof(ManufactoryDescriber.DescribeRecipe), typeof(RecipeSpecification))]
static class ManufactoryDescriberDescribeRecipePatch2 {
  static bool Prefix(RecipeSpecification productionRecipe,
                     ref bool __runOriginal, ref (VisualElement, VisualElement) __result,
                     ManufactoryDescriber __instance, ProductionItemFactory ____productionItemFactory,
                     GoodDescriber ____goodDescriber) {
    if (!__runOriginal) {
      return false; // The other patches must follow the same style to properly support the skip logic!
    }
    if (!productionRecipe.ConsumesFuel) {
      return true;
    }
    var inputs =
        ManufactoryDescriberDescribeRecipePatch1.GetAdjustedInputs(productionRecipe, __instance, ____goodDescriber);
    var input = ____productionItemFactory.CreateInput(inputs);
    var output = ____productionItemFactory.CreateOutput(__instance.GetOutputs(productionRecipe)); 
    __result = (input, output);
    return false;
  }
}

}
