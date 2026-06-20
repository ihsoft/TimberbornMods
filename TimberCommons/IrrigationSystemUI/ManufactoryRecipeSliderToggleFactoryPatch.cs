// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using IgorZ.TimberCommons.IrrigationSystem;
using Timberborn.SliderToggleSystem;
using Timberborn.Workshops;
using Timberborn.WorkshopsUI;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.IrrigationSystemUI;

/// <summary>Allows copied recipe specs to still show as selected in the compact recipe toggle.</summary>
[HarmonyPatch(typeof(ManufactoryRecipeSliderToggleFactory), nameof(ManufactoryRecipeSliderToggleFactory.Create))]
static class ManufactoryRecipeSliderToggleFactoryPatch {
  static bool Prefix(
      ManufactoryRecipeSliderToggleFactory __instance, VisualElement parent, Manufactory manufactory,
      ref SliderToggle __result, bool __runOriginal) {
    if (!__runOriginal) {
      return true;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (!manufactory.GetComponent<ManufactoryIrrigationTower>()) {
      return true;
    }
    __result = __instance._sliderToggleFactory.Create(parent, CreateItems(__instance, manufactory).ToArray());
    return false;
  }

  static IEnumerable<SliderToggleItem> CreateItems(ManufactoryRecipeSliderToggleFactory factory, Manufactory manufactory) {
    var enumerator = manufactory.ProductionRecipes.GetEnumerator();
    while (enumerator.MoveNext()) {
      var productionRecipe = enumerator.Current;
      yield return SliderToggleItem.Create(
          () => factory._loc.T(productionRecipe.DisplayLocKey),
          productionRecipe.UIIcon.Value,
          () => manufactory.SetRecipe(productionRecipe),
          () => manufactory.CurrentRecipe?.Id == productionRecipe.Id);
    }
  }
}
