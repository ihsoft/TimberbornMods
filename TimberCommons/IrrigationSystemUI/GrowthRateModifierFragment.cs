// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberCommons.IrrigationSystem;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.IrrigationSystemUI;

sealed class GrowthRateModifierFragment(UiFactory uiFactory) : IEntityPanelFragment {
  const string BoostPercentileLocKey = "IgorZ.TimberCommons.GrowthRateModifier.BoostPercentile";
  const string SlowdownPercentileLocKey = "IgorZ.TimberCommons.GrowthRateModifier.SlowdownPercentile";

  VisualElement _root;
  Label _infoLabel;

  GrowthRateModifier _growthModifier;

  public VisualElement InitializeFragment() {
    _infoLabel = uiFactory.CreateLabel();
    _root = uiFactory.CreateCenteredPanelFragment();
    _root.Add(_infoLabel);
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _growthModifier = entity.GetComponent<GrowthRateModifier>();
    _root.ToggleDisplayStyle(visible: IsModifierVisible());
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _growthModifier = null;
  }

  public void UpdateFragment() {
    if (!_root.visible || _growthModifier == null) {
      return;
    }
    _root.ToggleDisplayStyle(visible: IsModifierVisible());
    var locKey = _growthModifier.EffectiveModifier > 0f ? BoostPercentileLocKey : SlowdownPercentileLocKey;
    _infoLabel.text = uiFactory.T(locKey, Mathf.Abs(_growthModifier.EffectiveModifier));
  }

  bool IsModifierVisible() {
    return _growthModifier is { IsLiveAndGrowing: true, RateIsModified: true };
  }
}