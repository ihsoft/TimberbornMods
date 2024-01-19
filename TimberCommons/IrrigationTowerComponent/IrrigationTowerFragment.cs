// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using IgorZ.TimberDev.UI;
using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.GoodConsumingBuildingSystemUI;
using Timberborn.Localization;
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
[HarmonyPatch(typeof(GoodConsumingBuildingFragment), "UpdateProgressBar")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
static class GoodConsumingBuildingFragmentPatch {
  const string SupplyRemainingLocKey = "IgorZ.TimberCommons.WaterTower.SupplyRemaining";
  const float SwitchToDaysThreshold = 24f;

  [SuppressMessage("ReSharper", "InconsistentNaming")]
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

}
