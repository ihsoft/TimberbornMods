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

sealed class GrowthRateModifierFragment : IEntityPanelFragment {
  const string BoostPercentileLocKey = "IgorZ.TimberCommons.GrowthRateModifier.BoostPercentile";
  const string SlowdownPercentileLocKey = "IgorZ.TimberCommons.GrowthRateModifier.SlowdownPercentile";

  readonly UiFactory _uiFactory;
  
  VisualElement _root;
  Label _infoLabel;

  GrowthRateModifier _growthModifier;

  public GrowthRateModifierFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  public VisualElement InitializeFragment() {
    _infoLabel = _uiFactory.CreateLabel();
    _root = _uiFactory.CreateCenteredPanelFragmentBuilder().AddComponent(_infoLabel).BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _growthModifier = entity.GetComponentFast<GrowthRateModifier>();
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
    _infoLabel.text = _uiFactory.Loc.T(locKey, Mathf.Abs(_growthModifier.EffectiveModifier));
  }

  bool IsModifierVisible() {
    return _growthModifier != null && _growthModifier.IsLiveAndGrowing && _growthModifier.RateIsModified;
  }
}