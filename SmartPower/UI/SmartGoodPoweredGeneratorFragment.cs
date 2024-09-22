// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.SmartPower.Core;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.SmartPower.UI {

/// <summary>UI fragment that controls <see cref="SmartGoodPoweredGenerator"/> settings.</summary>
sealed class SmartGoodPoweredGeneratorFragment : IEntityPanelFragment {
  const string NeverStopThisGeneratorLocKey = "IgorZ.SmartPower.PoweredGenerator.NeverStop";
  const string ChargeLevelLocKey = "IgorZ.SmartPower.PoweredGenerator.ChargeBatteriesRangeText";
  const string ApplyToAllGeneratorsLocKey = "IgorZ.SmartPower.PoweredGenerator.ApplyToAllGenerators";
  const string AppliedToGeneratorsLocKey = "IgorZ.SmartPower.PoweredGenerator.AppliedToGenerators";

  readonly UiFactory _uiFactory;

  VisualElement _root;
  Toggle _neverShutdownCheckbox;
  Label _chargeBatteriesText;
  MinMaxSlider _chargeBatteriesSlider;
  SmartGoodPoweredGenerator _generator;
  Button _applyToAllEnginesButton;

  float _resetButtonCaptionTimestamp = -1;

  SmartGoodPoweredGeneratorFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  public VisualElement InitializeFragment() {
    _neverShutdownCheckbox = _uiFactory.CreateToggle(
        NeverStopThisGeneratorLocKey, _ => {
          _generator.NeverShutdown = _neverShutdownCheckbox.value;
          UpdateControls();
        });
    _neverShutdownCheckbox.style.marginBottom = 5;

    _chargeBatteriesSlider = _uiFactory.CreateMinMaxSlider(
        evt => {
          _generator.DischargeBatteriesThreshold = evt.newValue.x;
          _generator.ChargeBatteriesThreshold = evt.newValue.y;
          UpdateControls();
        }, 0f, 1.0f, 0.10f, stepSize: 0.05f);
    _chargeBatteriesSlider.style.marginBottom = 5;

    _chargeBatteriesText = _uiFactory.CreateLabel();
    _applyToAllEnginesButton = _uiFactory.CreateButton(ApplyToAllGeneratorsLocKey, ApplyToAllEngines);

    var center = new VisualElement {
        style = {
            justifyContent = Justify.Center,
            flexDirection = FlexDirection.Row
        }
    };
    center.Add(_applyToAllEnginesButton);

    _root = _uiFactory.CreateCenteredPanelFragmentBuilder()
        .AddComponent(_neverShutdownCheckbox)
        .AddComponent(_chargeBatteriesText)
        .AddComponent(_chargeBatteriesSlider)
        .AddComponent(center)
        .BuildAndInitialize();

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _generator = entity.GetComponentFast<SmartGoodPoweredGenerator>();
    if (_generator == null) {
      return;
    }
    _neverShutdownCheckbox.SetValueWithoutNotify(_generator.NeverShutdown);
    _chargeBatteriesSlider.SetValueWithoutNotify(
        new Vector2(_generator.DischargeBatteriesThreshold, _generator.ChargeBatteriesThreshold));
    UpdateControls();
    _root.ToggleDisplayStyle(visible: true);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _generator = null;
  }

  public void UpdateFragment() {
    if (!_generator) {
      return;
    }
    _applyToAllEnginesButton.ToggleDisplayStyle(visible: _generator.enabled);
    if (_resetButtonCaptionTimestamp < 0 || _resetButtonCaptionTimestamp > Time.unscaledTime) {
      return;
    }
    _resetButtonCaptionTimestamp = -1;
    _applyToAllEnginesButton.text = _uiFactory.Loc.T(ApplyToAllGeneratorsLocKey);
    _applyToAllEnginesButton.SetEnabled(true);
  }

  void UpdateControls() {
    _chargeBatteriesText.text = _uiFactory.Loc.T(
        ChargeLevelLocKey, Mathf.RoundToInt(_generator.DischargeBatteriesThreshold * 100),
        Mathf.RoundToInt(_generator.ChargeBatteriesThreshold * 100));
  }

  void ApplyToAllEngines() {
    var affectedGenerators = 0;
    foreach (var mechanicalNode in _generator.MechanicalGraph.Nodes) {
      var smartGenerator = mechanicalNode.GetComponentFast<SmartGoodPoweredGenerator>();
      if (!smartGenerator || smartGenerator == _generator) {
        continue;
      }
      affectedGenerators++;
      smartGenerator.ChargeBatteriesThreshold = _generator.ChargeBatteriesThreshold;
      smartGenerator.DischargeBatteriesThreshold = _generator.DischargeBatteriesThreshold;
    }
    _resetButtonCaptionTimestamp = Time.unscaledTime + 1.0f;
    _applyToAllEnginesButton.text = _uiFactory.Loc.T(AppliedToGeneratorsLocKey, affectedGenerators);
    _applyToAllEnginesButton.SetEnabled(false);
  }
}

}
