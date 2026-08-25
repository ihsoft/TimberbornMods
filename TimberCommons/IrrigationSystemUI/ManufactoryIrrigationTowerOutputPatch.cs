// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using IgorZ.TimberCommons.IrrigationSystem;
using Timberborn.Workshops;
using Timberborn.WorkshopsUI;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.IrrigationSystemUI;

/// <summary>Shows irrigation range as a virtual output in manufactory irrigation tower recipes.</summary>
[HarmonyPatch(typeof(ManufactoryDescriber), nameof(ManufactoryDescriber.GetOutputs))]
static class ManufactoryIrrigationTowerOutputPatch {
  static IrrigationTowerOutputFactory _irrigationTowerOutputFactory;

  public static void Initialize(IrrigationTowerOutputFactory irrigationTowerOutputFactory) {
    _irrigationTowerOutputFactory = irrigationTowerOutputFactory;
  }

  static void Postfix(
      RecipeSpec productionRecipe, bool __runOriginal, ManufactoryDescriber __instance,
      ref IEnumerable<VisualElement> __result) {
    if (!__runOriginal || _irrigationTowerOutputFactory == null) {
      return;
    }

    var irrigationTowerSpec = __instance.GetComponent<ManufactoryIrrigationTowerSpec>();
    if (irrigationTowerSpec == null) {
      return;
    }

    var outputs = __result.ToList();
    outputs.Add(_irrigationTowerOutputFactory.CreateIrrigationRangeOutput(irrigationTowerSpec.IrrigationRange));
    AddEffectOutputs(productionRecipe, __instance, irrigationTowerSpec, outputs);
    __result = outputs;
  }

  static void AddEffectOutputs(
      RecipeSpec productionRecipe, ManufactoryDescriber describer, ManufactoryIrrigationTowerSpec irrigationTowerSpec,
      ICollection<VisualElement> outputs) {
    var recipeEffectGroups = irrigationTowerSpec.Effects
        .Select(pair => pair.Split(['='], 2))
        .ToDictionary(k => k[0], v => v[1]);
    if (!recipeEffectGroups.TryGetValue(productionRecipe.Id, out var recipeEffectGroup)) {
      return;
    }

    var growthEffects = new List<ModifyGrowableGrowthRangeEffectSpec>();
    describer.GetComponents(growthEffects);
    foreach (var effect in growthEffects.Where(effect => effect.EffectGroup == recipeEffectGroup)) {
      outputs.Add(_irrigationTowerOutputFactory.CreateGrowthModifierOutput(effect.GrowthRateModifier));
    }

    var contaminationEffects = new List<BlockContaminationRangeEffectSpec>();
    describer.GetComponents(contaminationEffects);
    foreach (var _ in contaminationEffects.Where(effect => effect.EffectGroup == recipeEffectGroup)) {
      outputs.Add(_irrigationTowerOutputFactory.CreateContaminationBlockOutput());
    }
  }
}
