// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.SmartPower.PowerGenerators;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.SmartPower.PowerGeneratorsUI;

sealed class PowerOutputBalancerFragment : IEntityPanelFragment {
  const string AutomateLocKey = "IgorZ.SmartPower.PowerOutputBalancer.AutomateGenerator";
  const string ChargeLevelLocKey = "IgorZ.SmartPower.PowerOutputBalancer.ChargeBatteriesRangeText";
  const string ApplyToAllGeneratorsLocKey = "IgorZ.SmartPower.PowerOutputBalancer.ApplyToAllGenerators";
  const string AppliedToGeneratorsLocKey = "IgorZ.SmartPower.PowerOutputBalancer.AppliedToGenerators";

  readonly UiFactory _uiFactory;

  VisualElement _root;
  Toggle _automateCheckbox;
  Label _chargeBatteriesText;
  MinMaxSlider _chargeBatteriesSlider;
  Button _applyToAllGeneratorsButton;

  PowerOutputBalancer _balancer;
  float _resetButtonCaptionTimestamp = -1;

  PowerOutputBalancerFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  public VisualElement InitializeFragment() {
    _automateCheckbox = _uiFactory.CreateToggle(
        AutomateLocKey, _ => {
          _balancer.Automate = _automateCheckbox.value;
          UpdateControls();
        });

    _chargeBatteriesSlider = _uiFactory.CreateMinMaxSlider(
        _ => {
          _balancer.DischargeBatteriesThreshold = _chargeBatteriesSlider.value.x;
          _balancer.ChargeBatteriesThreshold = _chargeBatteriesSlider.value.y;
          UpdateControls();
        }, 0f, 1.0f, 0.10f, stepSize: 0.05f);

    _chargeBatteriesText = _uiFactory.CreateLabel();
    _applyToAllGeneratorsButton = _uiFactory.CreateButton(ApplyToAllGeneratorsLocKey, ApplyToAllGenerators);

    _root = _uiFactory.CreateCenteredPanelFragmentBuilder()
        .AddComponent(_automateCheckbox)
        .AddComponent(_chargeBatteriesText)
        .AddComponent(_chargeBatteriesSlider)
        .AddComponent(_uiFactory.CenterElement(_applyToAllGeneratorsButton))
        .BuildAndInitialize();

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _balancer = entity.GetComponentFast<PowerOutputBalancer>();
    if (!_balancer) {
      return;
    }
    _automateCheckbox.SetValueWithoutNotify(_balancer.Automate);
    _chargeBatteriesSlider.SetValueWithoutNotify(
        new Vector2(_balancer.DischargeBatteriesThreshold, _balancer.ChargeBatteriesThreshold));
    UpdateControls();
    _root.ToggleDisplayStyle(visible: true);
    _applyToAllGeneratorsButton.ToggleDisplayStyle(visible: _balancer.enabled);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _balancer = null;
  }

  public void UpdateFragment() {
    if (!_balancer) {
      return;
    }
    _applyToAllGeneratorsButton.ToggleDisplayStyle(visible: _balancer.enabled);
    if (_resetButtonCaptionTimestamp < 0 || _resetButtonCaptionTimestamp > Time.unscaledTime) {
      return;
    }
    _resetButtonCaptionTimestamp = -1;
    _applyToAllGeneratorsButton.text = _uiFactory.Loc.T(ApplyToAllGeneratorsLocKey);
    _applyToAllGeneratorsButton.SetEnabled(true);
  }

  void UpdateControls() {
    _chargeBatteriesText.text = _uiFactory.Loc.T(
        ChargeLevelLocKey, Mathf.RoundToInt(_balancer.DischargeBatteriesThreshold * 100),
        Mathf.RoundToInt(_balancer.ChargeBatteriesThreshold * 100));
  }

  void ApplyToAllGenerators() {
    var affectedGenerators = 0;
    foreach (var balancer in _balancer.AllBalancers.Where(x => x != _balancer)) {
      affectedGenerators++;
      balancer.ChargeBatteriesThreshold = _balancer.ChargeBatteriesThreshold;
      balancer.DischargeBatteriesThreshold = _balancer.DischargeBatteriesThreshold;
    }
    _resetButtonCaptionTimestamp = Time.unscaledTime + 1.0f;
    _applyToAllGeneratorsButton.text = _uiFactory.Loc.T(AppliedToGeneratorsLocKey, affectedGenerators);
    _applyToAllGeneratorsButton.SetEnabled(false);
  }
}
