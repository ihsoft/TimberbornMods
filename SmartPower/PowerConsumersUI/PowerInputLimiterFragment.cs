// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.SmartPower.PowerConsumers;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.SmartPower.PowerConsumersUI;

sealed class PowerInputLimiterFragment : IEntityPanelFragment {
  const string SuspendIfNoPowerLocKey = "IgorZ.SmartPower.PowerInputLimiter.SuspendIfNoPower";
  const string MinBatteriesRatioLocKey = "IgorZ.SmartPower.PowerInputLimiter.MinBatteriesRatio";
  const string ApplyToAllBuildingsLocKey = "IgorZ.SmartPower.PowerInputLimiter.ApplyToAllBuildings";
  const string AppliedToBuildingsLocKey = "IgorZ.SmartPower.PowerInputLimiter.AppliedToBuildings";

  readonly UiFactory _uiFactory;

  VisualElement _root;
  Toggle _suspendIfNoPowerCheckbox;
  Label _chargeBatteriesText;
  Slider _minBatteriesChargeSlider;
  Button _applyToAllGeneratorsButton;

  PowerInputLimiter _powerInputLimiter;
  float _resetButtonCaptionTimestamp = -1;

  PowerInputLimiterFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  public VisualElement InitializeFragment() {
    _suspendIfNoPowerCheckbox = _uiFactory.CreateToggle(
        SuspendIfNoPowerLocKey, e => {
          _powerInputLimiter.Automate = e.newValue;
          UpdateControls();
        });
    _minBatteriesChargeSlider = _uiFactory.CreateSlider(
        _ => {
          _powerInputLimiter.MinBatteriesCharge = _minBatteriesChargeSlider.value;
          UpdateControls();
        }, 0f, 1.0f, stepSize: 0.05f);

    _chargeBatteriesText = _uiFactory.CreateLabel();
    _applyToAllGeneratorsButton = _uiFactory.CreateButton(ApplyToAllBuildingsLocKey, ApplyToAllBuildings);

    _root = _uiFactory.CreateCenteredPanelFragmentBuilder()
        .AddComponent(_suspendIfNoPowerCheckbox)
        .AddComponent(_chargeBatteriesText)
        .AddComponent(_minBatteriesChargeSlider)
        .AddComponent(_uiFactory.CenterElement(_applyToAllGeneratorsButton))
        .BuildAndInitialize();
    
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _powerInputLimiter = entity.GetComponentFast<PowerInputLimiter>();
    if (_powerInputLimiter == null) {
      return;
    }
    _suspendIfNoPowerCheckbox.SetValueWithoutNotify(_powerInputLimiter.Automate);
    _minBatteriesChargeSlider.SetValueWithoutNotify(_powerInputLimiter.MinBatteriesCharge);
    UpdateControls();
    _root.ToggleDisplayStyle(visible: true);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _powerInputLimiter = null;
  }

  public void UpdateFragment() {
    if (!_powerInputLimiter) {
      return;
    }
    _applyToAllGeneratorsButton.ToggleDisplayStyle(visible: _powerInputLimiter.enabled);
    if (_resetButtonCaptionTimestamp < 0 || _resetButtonCaptionTimestamp > Time.unscaledTime) {
      return;
    }
    _resetButtonCaptionTimestamp = -1;
    _applyToAllGeneratorsButton.text = _uiFactory.Loc.T(ApplyToAllBuildingsLocKey);
    _applyToAllGeneratorsButton.SetEnabled(true);
  }

  void UpdateControls() {
    _chargeBatteriesText.text = _uiFactory.Loc.T(
      MinBatteriesRatioLocKey, Mathf.RoundToInt(_powerInputLimiter.MinBatteriesCharge * 100));
  }

  void ApplyToAllBuildings() {
    var affectedBuildings = 0;
    foreach (var balancer in _powerInputLimiter.AllLimiters.Where(x => x != _powerInputLimiter)) {
      affectedBuildings++;
      balancer.MinBatteriesCharge = _powerInputLimiter.MinBatteriesCharge;
    }
    _resetButtonCaptionTimestamp = Time.unscaledTime + 1.0f;
    _applyToAllGeneratorsButton.text = _uiFactory.Loc.T(AppliedToBuildingsLocKey, affectedBuildings);
    _applyToAllGeneratorsButton.SetEnabled(false);
  }
}
