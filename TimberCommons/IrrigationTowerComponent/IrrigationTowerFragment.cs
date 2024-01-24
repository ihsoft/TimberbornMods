// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.UI;
using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.GoodConsumingBuildingSystemUI;
using Timberborn.GoodsUI;
using Timberborn.Localization;
using Timberborn.UIFormatters;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.IrrigationTowerComponent {

sealed class IrrigationTowerFragment : IEntityPanelFragment {
  const string IrrigationCoverageLocKey = "IgorZ.TimberCommons.WaterTower.IrrigationCoverage";
  const string IrrigatedAreaLocKey = "IgorZ.TimberCommons.WaterTower.IrrigatedArea";
  const string EffectiveRangeLocKey = "IgorZ.TimberCommons.WaterTower.EffectiveRange";

  readonly UIBuilder _builder;
  readonly ILoc _loc;
  
  VisualElement _root;
  Label _infoLabel;

  IrrigationTower _irrigationTower;

  public IrrigationTowerFragment(UIBuilder builder, ILoc loc) {
    _builder = builder;
    _loc = loc;
  }

  public VisualElement InitializeFragment() {
    _infoLabel = _builder.Presets().Labels().Label(color: UiFactory.PanelNormalColor);
    _root = _builder.CreateFragmentBuilder().AddComponent(_infoLabel).BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _irrigationTower = entity.GetComponentFast<IrrigationTower>();
    _root.ToggleDisplayStyle(visible: _irrigationTower != null);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _irrigationTower = null;
  }

  public void UpdateFragment() {
    if (_irrigationTower == null) {
      return;
    }
    if (_irrigationTower.enabled) {
      var coveragePct = _irrigationTower.EligibleTilesCount * 100f / _irrigationTower.MaxCoveredTilesCount;
      var info = new List<string> {
          _loc.T(IrrigationCoverageLocKey, coveragePct),
          _loc.T(IrrigatedAreaLocKey, _irrigationTower.IrrigatedTilesCount),
          _loc.T(EffectiveRangeLocKey, _irrigationTower.EffectiveRange),
      };
      _infoLabel.text = string.Join("\n", info);
    }
    _infoLabel.ToggleDisplayStyle(visible: _irrigationTower.enabled);
  }
}

#region Harmony patch to show supply in days and hours.
[HarmonyPatch(typeof(GoodConsumingBuildingFragment), nameof(GoodConsumingBuildingFragment.UpdateProgressBar))]
static class GoodConsumingBuildingFragmentPatch {
  const string SupplyRemainingLocKey = "IgorZ.TimberCommons.WaterTower.SupplyRemaining";
  const float SwitchToDaysThreshold = 24f;

  [SuppressMessage("ReSharper", "InconsistentNaming")]
  // ReSharper disable once UnusedMember.Local
  static void Postfix(ref bool __runOriginal, ILoc ____loc, Label ____hoursLeft,
                      GoodConsumingBuilding ____goodConsumingBuilding) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (____goodConsumingBuilding.HoursUntilNoSupply > SwitchToDaysThreshold) {
      var duration = Mathf.RoundToInt(____goodConsumingBuilding.HoursUntilNoSupply);
      ____hoursLeft.text = ____loc.T(SupplyRemainingLocKey, duration / 24, duration % 24);
    }
  }
}
#endregion

#region Harmony patch to improve consumption rate fromatting
[HarmonyPatch(typeof(GoodConsumingBuildingDescriber), nameof(GoodConsumingBuildingDescriber.DescribeSupply))]
static class GoodConsumingBuildingDescriberPatch {
  const string DescriptionLocKey = "GoodConsuming.SupplyDescription";

  [SuppressMessage("ReSharper", "InconsistentNaming")]
  // ReSharper disable once UnusedMember.Local
  static void Postfix(ref bool __runOriginal, ref EntityDescription __result,
                      ILoc ____loc, GoodDescriber ____goodDescriber,
                      DescribedAmountFactory ____describedAmountFactory,
                      ResourceAmountFormatter ____resourceAmountFormatter,
                      ProductionItemFactory ____productionItemFactory,
                      GoodConsumingBuilding ____goodConsumingBuilding) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }

    var formatters = new List<IConsumptionRateFormatter>();
    ____goodConsumingBuilding.GetComponentsFast(formatters);
    if (formatters.Count > 0) {
      var formatter = formatters[0];
      var describedGood = ____goodDescriber.GetDescribedGood(____goodConsumingBuilding.Supply);
      var param = ____resourceAmountFormatter.FormatPerHour(
          describedGood.DisplayName, ____goodConsumingBuilding.GoodPerHour);
      var tooltip = ____loc.T(DescriptionLocKey, param);
      var input = ____describedAmountFactory.CreatePlain("", formatter.GetRate(), describedGood.Icon, tooltip);
      var content = ____productionItemFactory.CreateInput(input);
      __result = EntityDescription.CreateInputSectionWithTime(content, int.MaxValue, formatter.GetTime());
    }
  }
}
#endregion

}
