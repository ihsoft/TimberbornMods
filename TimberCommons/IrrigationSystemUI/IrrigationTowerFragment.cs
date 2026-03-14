// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.TimberCommons.IrrigationSystem;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.IrrigationSystemUI;

sealed class IrrigationTowerFragment : IEntityPanelFragment {
  const string TowerUtilizationLocKey = "IgorZ.TimberCommons.WaterTower.Utilization";
  const string IrrigatedAreaLocKey = "IgorZ.TimberCommons.WaterTower.IrrigatedArea";
  const string EffectiveRangeLocKey = "IgorZ.TimberCommons.WaterTower.EffectiveRange";

  readonly UiFactory _uiFactory;
  readonly ILoc _loc;
  
  VisualElement _root;
  Label _infoLabel;

  IrrigationTower _irrigationTower;

  IrrigationTowerFragment(UiFactory uiFactory, ILoc loc) {
    _uiFactory = uiFactory;
    _loc = loc;
  }

  public VisualElement InitializeFragment() {
    _infoLabel = _uiFactory.CreateLabel();
    _root = _uiFactory.CreateCenteredPanelFragment();
    _root.Add(_infoLabel);
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _irrigationTower = entity.GetComponent<IrrigationTower>();
    _root.ToggleDisplayStyle(visible: _irrigationTower);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _irrigationTower = null;
  }

  public void UpdateFragment() {
    if (!_irrigationTower) {
      return;
    }
    if (_irrigationTower.Enabled) {
      var utilization = _irrigationTower.EligibleTiles.Count * 100f / _irrigationTower.MaxCoveredTilesCount;
      var info = new List<string> {
          _loc.T(TowerUtilizationLocKey, utilization),
          _loc.T(IrrigatedAreaLocKey, _irrigationTower.ReachableTiles.Count),
          _loc.T(EffectiveRangeLocKey, _irrigationTower.EffectiveRange),
      };
      _infoLabel.text = string.Join("\n", info);
    }
    _infoLabel.ToggleDisplayStyle(visible: _irrigationTower.Enabled);
  }
}