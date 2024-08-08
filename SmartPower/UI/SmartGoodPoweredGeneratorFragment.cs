// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower.UI {

/// <summary>UI fragment that controls <see cref="SmartGoodPoweredGenerator"/> settings.</summary>
sealed class SmartGoodPoweredGeneratorFragment : IEntityPanelFragment {
  const string NeverStopThisGeneratorLocKey = "IgorZ.SmartPower.PoweredGenerator.NeverStop";
  const string ChargeLevelLocKey = "IgorZ.SmartPower.PoweredGenerator.ChargeBatteriesRangeText";
  const float BatteriesChargeRangeSize = 0.25f;

  readonly UiFactory _uiFactory;

  VisualElement _root;
  Toggle _neverShutdownCheckbox;
  Label _chargeBatteriesText;
  Slider _chargeBatteriesSlider;
  SmartGoodPoweredGenerator _generator;

  public SmartGoodPoweredGeneratorFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  public VisualElement InitializeFragment() {
    _neverShutdownCheckbox = _uiFactory.CreateToggle(
        NeverStopThisGeneratorLocKey, _ => {
          _generator.NeverShutdown = _neverShutdownCheckbox.value;
          UpdateControls();
        });

    _chargeBatteriesSlider = _uiFactory.CreateSlider(
        value => {
          _generator.DischargeBatteriesThreshold = value;
          _generator.ChargeBatteriesThreshold = value + BatteriesChargeRangeSize;
          UpdateControls();
        }, lowValue: 0.1f, highValue: 0.9f - BatteriesChargeRangeSize);

    _chargeBatteriesText = _uiFactory.CreateLabel();
    _chargeBatteriesText.style.marginTop = 5;

    _root = _uiFactory.CreateFragmentPanel();
    _root.Add(_neverShutdownCheckbox);
    _root.Add(_chargeBatteriesText);
    _root.Add(_chargeBatteriesSlider);

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _generator = entity.GetComponentFast<SmartGoodPoweredGenerator>();
    if (_generator != null) {
      _neverShutdownCheckbox.SetValueWithoutNotify(_generator.NeverShutdown);
      _chargeBatteriesSlider.SetValueWithoutNotify(_generator.DischargeBatteriesThreshold);
      UpdateControls();
      _root.ToggleDisplayStyle(visible: true);
    }
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _generator = null;
  }

  public void UpdateFragment() {
  }

  void UpdateControls() {
    _chargeBatteriesText.text = _uiFactory.Loc.T(
        ChargeLevelLocKey, Mathf.RoundToInt(_generator.DischargeBatteriesThreshold * 100),
        Mathf.RoundToInt(_generator.ChargeBatteriesThreshold * 100));
  }
}

}
