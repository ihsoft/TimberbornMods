// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.SmartPower.PowerConsumers;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.SmartPower.PowerConsumersUI;

sealed class PowerInputLimiterFragment : IEntityPanelFragment {
  const string SuspendIfNoPowerLocKey = "IgorZ.SmartPower.PowerInputLimiter.SuspendIfNoPower";
  const string SuspendIfLowEfficiencyLocKey = "IgorZ.SmartPower.PowerInputLimiter.SuspendIfLowEfficiency";
  const string MinBatteriesRatioLocKey = "IgorZ.SmartPower.PowerInputLimiter.MinBatteriesRatio";
  const string ApplyToAllBuildingsLocKey = "IgorZ.SmartPower.PowerInputLimiter.ApplyToAllBuildings";
  const string AppliedToBuildingsLocKey = "IgorZ.SmartPower.PowerInputLimiter.AppliedToBuildings";

  readonly UiFactory _uiFactory;
  readonly ConsumerFragmentPatcher _consumerFragmentPatcher;

  VisualElement _root;
  Toggle _automateCheckbox;
  Label _suspendIfLowEfficiencyLabel;
  PreciseSliderWrapper _minEfficiencySlider;
  Toggle _suspendIfBatteryLowCheckbox;
  Slider _minBatteriesChargeSlider;
  Button _applyToAllBuildingsButton;

  PowerInputLimiter _powerInputLimiter;
  TimedUpdater _applyToAllUpdater;

  PowerInputLimiterFragment(UiFactory uiFactory, ConsumerFragmentPatcher consumerFragmentPatcher) {
    _uiFactory = uiFactory;
    _consumerFragmentPatcher = consumerFragmentPatcher;
  }

  public VisualElement InitializeFragment() {
    _automateCheckbox = _uiFactory.CreateToggle(
        SuspendIfNoPowerLocKey, e => {
          _powerInputLimiter.Automate = e.newValue;
          _powerInputLimiter.UpdateState();
          UpdateControls();
        });
    _suspendIfLowEfficiencyLabel = _uiFactory.CreateLabel();
    _minEfficiencySlider = _uiFactory.CreatePreciseSlider(
        0.01f,
        newValue => {
          _powerInputLimiter.MinPowerEfficiency = newValue;
          _powerInputLimiter.UpdateState();
          UpdateControls();
        });
    _suspendIfBatteryLowCheckbox = _uiFactory.CreateToggle(
        null, e => {
          _powerInputLimiter.CheckBatteryCharge = e.newValue;
          _powerInputLimiter.UpdateState();
          UpdateControls();
        });
    _minBatteriesChargeSlider = _uiFactory.CreateSlider(
        e => {
          _powerInputLimiter.MinBatteriesCharge = e.newValue;
          _powerInputLimiter.UpdateState();
          UpdateControls();
        }, 0f, 1.0f, stepSize: 0.05f);

    _applyToAllBuildingsButton = _uiFactory.CreateButton(ApplyToAllBuildingsLocKey, ApplyToAllBuildings);

    _root = _uiFactory.CreateCenteredPanelFragment();
    _root.Add(_automateCheckbox);
    _root.Add(_suspendIfLowEfficiencyLabel);
    _root.Add(_minEfficiencySlider);
    _root.Add(_suspendIfBatteryLowCheckbox);
    _root.Add(_minBatteriesChargeSlider);
    _root.Add(_uiFactory.CenterElement(_applyToAllBuildingsButton));

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _consumerFragmentPatcher.InitializePatch(_root);
    _powerInputLimiter = entity.GetComponentFast<PowerInputLimiter>();
    if (!_powerInputLimiter) {
      return;
    }
    _automateCheckbox.SetValueWithoutNotify(_powerInputLimiter.Automate);
    _minEfficiencySlider.UpdateValuesWithoutNotify(_powerInputLimiter.MinPowerEfficiency, 1f);
    _suspendIfBatteryLowCheckbox.SetValueWithoutNotify(_powerInputLimiter.CheckBatteryCharge);
    _minBatteriesChargeSlider.SetValueWithoutNotify(_powerInputLimiter.MinBatteriesCharge);
    UpdateControls();
    _root.ToggleDisplayStyle(visible: true);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _consumerFragmentPatcher.HideAllElements();
    _powerInputLimiter = null;
  }

  public void UpdateFragment() {
    if (!_powerInputLimiter) {
      return;
    }
    _applyToAllBuildingsButton.ToggleDisplayStyle(visible: _powerInputLimiter.enabled);
    _applyToAllUpdater?.Update(
        () => {
          _applyToAllBuildingsButton.text = _uiFactory.T(ApplyToAllBuildingsLocKey);
          _applyToAllBuildingsButton.SetEnabled(true);
          _applyToAllUpdater = null;
        });
    _consumerFragmentPatcher.UpdatePowerInputLimiter(_powerInputLimiter);
  }

  void UpdateControls() {
    _suspendIfLowEfficiencyLabel.text = _uiFactory.T(
        SuspendIfLowEfficiencyLocKey, Mathf.RoundToInt(_minEfficiencySlider.Value * 100));
    _suspendIfBatteryLowCheckbox.text = _uiFactory.T(
        MinBatteriesRatioLocKey, Mathf.RoundToInt(_powerInputLimiter.MinBatteriesCharge * 100));

    _suspendIfLowEfficiencyLabel.SetEnabled(_automateCheckbox.value);
    _minEfficiencySlider.SetEnabled(_automateCheckbox.value);
    _suspendIfBatteryLowCheckbox.SetEnabled(_automateCheckbox.value);
    _minBatteriesChargeSlider.SetEnabled(_automateCheckbox.value && _suspendIfBatteryLowCheckbox.value);
  }

  void ApplyToAllBuildings() {
    var affectedBuildings = 0;
    foreach (var balancer in _powerInputLimiter.AllLimiters.Where(x => x != _powerInputLimiter)) {
      affectedBuildings++;
      balancer.MinPowerEfficiency = _powerInputLimiter.MinPowerEfficiency;
      balancer.CheckBatteryCharge = _powerInputLimiter.CheckBatteryCharge;
      balancer.MinBatteriesCharge = _powerInputLimiter.MinBatteriesCharge;
      balancer.UpdateState();
    }
    _applyToAllUpdater = new TimedUpdater(1.0f, startNow: true);
    _applyToAllBuildingsButton.text = _uiFactory.T(AppliedToBuildingsLocKey, affectedBuildings);
    _applyToAllBuildingsButton.SetEnabled(false);
  }
}
