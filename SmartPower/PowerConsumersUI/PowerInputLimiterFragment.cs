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
  const string SuspendIfLowEfficiencyLocKey = "IgorZ.SmartPower.PowerInputLimiter.SuspendIfLowEfficiency";
  const string MinBatteriesRatioLocKey = "IgorZ.SmartPower.PowerInputLimiter.MinBatteriesRatio";
  const string ApplyToAllBuildingsLocKey = "IgorZ.SmartPower.PowerInputLimiter.ApplyToAllBuildings";
  const string AppliedToBuildingsLocKey = "IgorZ.SmartPower.PowerInputLimiter.AppliedToBuildings";

  readonly UiFactory _uiFactory;
  readonly ConsumerFragmentPatcher _consumerFragmentPatcher;

  VisualElement _root;
  Toggle _automateCheckbox;
  Label _suspendIfLowEfficiencyLabel;
  PreciseSlider _minEfficiencySlider;
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
    _root = _uiFactory.LoadVisualTreeAsset("IgorZ/PowerInputLimiterFragment");
    _automateCheckbox = _root.Q<Toggle>("AutomateCheckbox");
    _automateCheckbox.RegisterValueChangedCallback(
        e => {
          _powerInputLimiter.Automate = e.newValue;
          _powerInputLimiter.UpdateState();
          UpdateControls();
        });
    _suspendIfLowEfficiencyLabel = _root.Q<Label>("SuspendIfLowEfficiencyLabel");
    _minEfficiencySlider = _root.Q<PreciseSlider>("MinEfficiencySlider");
    _minEfficiencySlider.UpdateValuesWithoutNotify(0, 1f);
    _uiFactory.AddFixedStepChangeHandler(
        _minEfficiencySlider, 0.01f,
        newValue => {
          _powerInputLimiter.MinPowerEfficiency = newValue;
          _powerInputLimiter.UpdateState();
          UpdateControls();
        });
    _suspendIfBatteryLowCheckbox = _root.Q<Toggle>("SuspendIfBatteryLowCheckbox");
    _suspendIfBatteryLowCheckbox.RegisterValueChangedCallback(
        e => {
          _powerInputLimiter.CheckBatteryCharge = e.newValue;
          _powerInputLimiter.UpdateState();
          UpdateControls();
        });
    _minBatteriesChargeSlider = _root.Q<Slider>("MinBatteriesChargeSlider");
    _uiFactory.AddFixedStepChangeHandler(
        _minBatteriesChargeSlider, 0.05f,
        newValue => {
          _powerInputLimiter.MinBatteriesCharge = newValue;
          _powerInputLimiter.UpdateState();
          UpdateControls();
        });

    _applyToAllBuildingsButton = _root.Q<Button>("ApplyToAllBuildingsButton");
    _applyToAllBuildingsButton.clicked += ApplyToAllBuildings;

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _consumerFragmentPatcher.InitializePatch(_root);
    _powerInputLimiter = entity.GetComponent<PowerInputLimiter>();
    if (!_powerInputLimiter) {
      return;
    }
    _automateCheckbox.SetValueWithoutNotify(_powerInputLimiter.Automate);
    _minEfficiencySlider.SetValueWithoutNotify(_powerInputLimiter.MinPowerEfficiency);
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
    _applyToAllBuildingsButton.ToggleDisplayStyle(visible: _powerInputLimiter.Enabled);
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
      balancer.DuplicateFrom(_powerInputLimiter);
    }
    _applyToAllUpdater = new TimedUpdater(1.0f, startNow: true);
    _applyToAllBuildingsButton.text = _uiFactory.T(AppliedToBuildingsLocKey, affectedBuildings);
    _applyToAllBuildingsButton.SetEnabled(false);
  }
}
