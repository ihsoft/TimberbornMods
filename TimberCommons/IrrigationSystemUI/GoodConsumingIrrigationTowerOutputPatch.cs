// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using IgorZ.TimberCommons.IrrigationSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.GoodConsumingBuildingSystemUI;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.IrrigationSystemUI;

/// <summary>Shows irrigation range as a virtual output in good consuming irrigation towers.</summary>
[HarmonyPatch(typeof(GoodConsumingBuildingDescriber), nameof(GoodConsumingBuildingDescriber.DescribeSupply))]
static class GoodConsumingIrrigationTowerOutputPatch {
  static IrrigationTowerOutputFactory _irrigationTowerOutputFactory;

  public static void Initialize(IrrigationTowerOutputFactory irrigationTowerOutputFactory) {
    _irrigationTowerOutputFactory = irrigationTowerOutputFactory;
  }

  [HarmonyPriority(Priority.Last)]
  static void Postfix(
      bool __runOriginal, ProductionItemFactory ____productionItemFactory, BaseComponent ____goodConsumingBuilding,
      ref EntityDescription __result) {
    if (!__runOriginal || _irrigationTowerOutputFactory == null) {
      return;
    }

    var irrigationTowerSpec = ____goodConsumingBuilding.GetComponent<GoodConsumingIrrigationTowerSpec>();
    if (irrigationTowerSpec == null) {
      return;
    }

    var inputs = __result.Section.Q<VisualElement>("Input").Children().ToList();
    var content = ____productionItemFactory.CreateInputOutput(
        inputs, CreateOutputs(____goodConsumingBuilding, irrigationTowerSpec), __result.Time);
    __result = EntityDescription.CreateInputOutputSection(content, int.MaxValue);
  }

  static IEnumerable<VisualElement> CreateOutputs(BaseComponent component, GoodConsumingIrrigationTowerSpec spec) {
    yield return _irrigationTowerOutputFactory.CreateIrrigationRangeOutput(spec.IrrigationRange);

    var growthEffects = new List<ModifyGrowableGrowthRangeEffectSpec>();
    component.GetComponents(growthEffects);
    foreach (var growthRateModifier in growthEffects.Select(effect => effect.GrowthRateModifier).Distinct()) {
      yield return _irrigationTowerOutputFactory.CreateGrowthModifierOutput(growthRateModifier);
    }

    var contaminationEffects = new List<BlockContaminationRangeEffectSpec>();
    component.GetComponents(contaminationEffects);
    if (contaminationEffects.Count > 0) {
      yield return _irrigationTowerOutputFactory.CreateContaminationBlockOutput();
    }
  }
}
