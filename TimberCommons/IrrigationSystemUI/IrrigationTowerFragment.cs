// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.TimberCommons.IrrigationSystem;
using IgorZ.TimberDev.UI;
using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.IrrigationSystemUI {

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

}
