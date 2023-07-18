// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower.UI {

/// <summary>UI fragment that controls <see cref="SmartGoodPoweredGenerator"/> settings.</summary>
sealed class SmartGoodPoweredGeneratorFragment : IEntityPanelFragment {
  const string NeverStopThisGeneratorLocKey = "IgorZ.SmartPower.PoweredGenerator.NeverStop";
  const string ChargeLevelLocKey = "Charge batteries to {0}%";
  static readonly Color NormalColor = new(0.8f, 0.8f, 0.8f);

  readonly UIBuilder _builder;
  readonly ILoc _loc;
  readonly VisualElementLoader _visualElementLoader;

  VisualElement _root;
  Toggle _neverShutdownCheckbox;
  Label _chargeBatteriesText;
  Slider _chargeBatteriesSlider;
  SmartGoodPoweredGenerator _generator;

  public SmartGoodPoweredGeneratorFragment(UIBuilder builder, ILoc loc, VisualElementLoader visualElementLoader) {
    _builder = builder;
    _loc = loc;
    _visualElementLoader = visualElementLoader;
  }

  public VisualElement InitializeFragment() {
    _neverShutdownCheckbox = _builder.Presets().Toggles()
        .CheckmarkInverted(locKey: NeverStopThisGeneratorLocKey, color: NormalColor);
    _neverShutdownCheckbox.RegisterValueChangedCallback(
        _ => {
          _generator.NeverShutdown = _neverShutdownCheckbox.value;
          UpdateControls();
        });

    _chargeBatteriesSlider = _visualElementLoader.LoadVisualElement("Common/IntegerSlider").Q<Slider>("Slider");
    _chargeBatteriesSlider.lowValue = 0.0f;
    _chargeBatteriesSlider.highValue = 1.0f;
    _chargeBatteriesSlider.RegisterValueChangedCallback(
        _ => {
          _generator.ChargeBatteriesThreshold = _chargeBatteriesSlider.value;
          UpdateControls();
        });

    _chargeBatteriesText = _builder.Presets().Labels().Label(color: NormalColor);

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_neverShutdownCheckbox)
        .AddComponent(_chargeBatteriesText)
        .AddComponent(_chargeBatteriesSlider)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _generator = entity.GetComponentFast<SmartGoodPoweredGenerator>();
    if (_generator != null) {
      _neverShutdownCheckbox.SetValueWithoutNotify(_generator.NeverShutdown);
      _chargeBatteriesSlider.SetValueWithoutNotify(_generator.ChargeBatteriesThreshold);
      UpdateControls();
      _root.ToggleDisplayStyle(visible: true);
    } else {
      _root.ToggleDisplayStyle(visible: false);
    }
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _generator = null;
  }

  public void UpdateFragment() {
  }

  void UpdateControls() {
    if (_generator.NeverShutdown) {
      _chargeBatteriesText.SetEnabled(false);
      _chargeBatteriesSlider.SetEnabled(false);
      _chargeBatteriesSlider.SetValueWithoutNotify(1.0f);
      _generator.ChargeBatteriesThreshold = 1.0f;
    } else {
      _chargeBatteriesText.SetEnabled(true);
      _chargeBatteriesSlider.SetEnabled(true);
    }
    _chargeBatteriesText.text = _loc.T(ChargeLevelLocKey, Mathf.RoundToInt(_chargeBatteriesSlider.value * 100));
  }
}

}
