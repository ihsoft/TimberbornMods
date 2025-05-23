﻿// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using IgorZ.TimberCommons.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.Workshops;
using Timberborn.WorkshopsUI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches;

/// <summary>Harmony patch to show low fuel ingredient consumption rate in the recipe UIs.</summary>
[HarmonyPatch(typeof(ManufactoryDescriber), nameof(ManufactoryDescriber.GetInputs))]
static class ManufactoryDescriberPatch2 {
  static void Postfix(RecipeSpec productionRecipe, bool __runOriginal, ref IEnumerable<VisualElement> __result) {
    if (!__runOriginal) {
      return; // The other patches must follow the same style to properly support the skip logic!
    }
    if (!productionRecipe.ConsumesFuel || !TimeAndDurationSettings.HigherPrecisionForFuelConsumingRecipes) {
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