// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.UI;
using TimberApi.UIPresets.Buttons;
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

  static readonly Vector2 LessChargeRange = new(0.30f, 0.55f);
  static readonly Vector2 MoreChargeRange = new(0.65f, 0.90f);

  readonly UiFactory _uiFactory;

  VisualElement _root;
  Toggle _neverShutdownCheckbox;
  Label _chargeBatteriesText;
  Button _lessChargeButton;
  Button _moreChargeButton;
  MinMaxSlider _chargeBatteriesSlider;
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

    _chargeBatteriesSlider = _uiFactory.CreateMinMaxSlider(
        evt => {
          _generator.DischargeBatteriesThreshold = evt.newValue.x;
          _generator.ChargeBatteriesThreshold = evt.newValue.y;
          UpdateControls();
        }, 0f, 1.0f, 0.10f, stepSize: 0.05f);
    _chargeBatteriesSlider.style.marginLeft = 10;
    _chargeBatteriesSlider.style.marginRight = 10;

    _chargeBatteriesText = _uiFactory.CreateLabel();
    _chargeBatteriesText.style.marginTop = 5;
    _chargeBatteriesText.style.marginBottom = 5;
    
    _lessChargeButton = _uiFactory.UiBuilder.Create<ArrowLeftButton>().Build();
    _lessChargeButton.clicked += () => {
      _chargeBatteriesSlider.value = LessChargeRange;
    };
    _moreChargeButton = _uiFactory.UiBuilder.Create<ArrowRightButton>().Build();
    _moreChargeButton.clicked += () => {
      _chargeBatteriesSlider.value = MoreChargeRange;
    };

    var panel = new VisualElement();
    panel.style.flexDirection = FlexDirection.Row;
    panel.Add(_lessChargeButton);
    panel.Add(_chargeBatteriesSlider);
    panel.Add(_moreChargeButton);

    _root = _uiFactory.CreateCenteredPanelFragmentBuilder()
        .AddComponent(_neverShutdownCheckbox)
        .AddComponent(_chargeBatteriesText)
        .AddComponent(panel)
        .BuildAndInitialize();

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _generator = entity.GetComponentFast<SmartGoodPoweredGenerator>();
    if (_generator != null) {
      _neverShutdownCheckbox.SetValueWithoutNotify(_generator.NeverShutdown);
      _chargeBatteriesSlider.SetValueWithoutNotify(
          new Vector2(_generator.DischargeBatteriesThreshold, _generator.ChargeBatteriesThreshold));
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
    _moreChargeButton.SetEnabled(_chargeBatteriesSlider.value != MoreChargeRange);
    _lessChargeButton.SetEnabled(_chargeBatteriesSlider.value != LessChargeRange);
  }
}

}
