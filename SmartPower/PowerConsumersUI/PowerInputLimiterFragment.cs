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

  Label _stateProgressLabel;  // It is patched in the stock UI.
  PanelFragmentPatcher _stateProgressPatcher;
  Label _suspendReasonLabel;  // It is patched in the stock UI.
  PanelFragmentPatcher _suspendReasonPatcher;
  
  VisualElement _root;
  Toggle _automateCheckbox;
  Label _suspendIfLowEfficiencyLabel;
  PreciseSliderWrapper _minEfficiencySlider;
  Toggle _suspendIfBatteryLowCheckbox;
  Slider _minBatteriesChargeSlider;
  Button _applyToAllBuildingsButton;

  PowerInputLimiter _powerInputLimiter;
  TimedUpdater _applyToAllUpdater;

  PowerInputLimiterFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
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

    _root = _uiFactory.CreateCenteredPanelFragmentBuilder()
        .AddComponent(_automateCheckbox)
        .AddComponent(_suspendIfLowEfficiencyLabel)
        .AddComponent(_minEfficiencySlider)
        .AddComponent(_suspendIfBatteryLowCheckbox)
        .AddComponent(_minBatteriesChargeSlider)
        .AddComponent(_uiFactory.CenterElement(_applyToAllBuildingsButton))
        .BuildAndInitialize();

    _suspendReasonLabel = _uiFactory.CreateLabel();
    _suspendReasonLabel.ToggleDisplayStyle(visible: false);
    _suspendReasonPatcher = new PanelFragmentPatcher(
        _suspendReasonLabel, _root, PanelFragmentPatcher.MechanicalNodeFragmentName, "Consumer");

    _stateProgressLabel = _uiFactory.CreateLabel();
    _stateProgressLabel.ToggleDisplayStyle(visible: false);
    _stateProgressPatcher = new PanelFragmentPatcher(
        _stateProgressLabel, _root, PanelFragmentPatcher.MechanicalNodeFragmentName, "Consumer", 1);

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _powerInputLimiter = entity.GetComponentFast<PowerInputLimiter>();
    if (_powerInputLimiter == null) {
      return;
    }
    _suspendReasonPatcher.Patch();
    _stateProgressPatcher.Patch();
    _automateCheckbox.SetValueWithoutNotify(_powerInputLimiter.Automate);
    _minEfficiencySlider.UpdateValuesWithoutNotify(_powerInputLimiter.MinPowerEfficiency, 1f);
    _suspendIfBatteryLowCheckbox.SetValueWithoutNotify(_powerInputLimiter.CheckBatteryCharge);
    _minBatteriesChargeSlider.SetValueWithoutNotify(_powerInputLimiter.MinBatteriesCharge);
    UpdateControls();
    _root.ToggleDisplayStyle(visible: true);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _suspendReasonLabel.ToggleDisplayStyle(visible: false);
    _stateProgressLabel.ToggleDisplayStyle(visible: false);
    _powerInputLimiter = null;
  }

  public void UpdateFragment() {
    if (!_powerInputLimiter) {
      return;
    }
    _applyToAllBuildingsButton.ToggleDisplayStyle(visible: _powerInputLimiter.enabled);
    _applyToAllUpdater?.Update(
        () => {
          _applyToAllBuildingsButton.text = _uiFactory.Loc.T(ApplyToAllBuildingsLocKey);
          _applyToAllBuildingsButton.SetEnabled(true);
          _applyToAllUpdater = null;
        });
    UpdateConsumerBuildingText();
  }

  void UpdateControls() {
    _suspendIfLowEfficiencyLabel.text = _uiFactory.Loc.T(
        SuspendIfLowEfficiencyLocKey, Mathf.RoundToInt(_minEfficiencySlider.Value * 100));
    _suspendIfBatteryLowCheckbox.text = _uiFactory.Loc.T(
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
    _applyToAllBuildingsButton.text = _uiFactory.Loc.T(AppliedToBuildingsLocKey, affectedBuildings);
    _applyToAllBuildingsButton.SetEnabled(false);
  }

  const string NotEnoughPowerLocKey = "IgorZ.SmartPower.PowerInputLimiter.NotEnoughPowerStatus";
  const string LowBatteriesChargeLocKey = "IgorZ.SmartPower.PowerInputLimiter.LowBatteriesChargeStatus";
  const string MinutesTillResumeLocKey = "IgorZ.SmartPower.Common.MinutesTillResume";
  const string MinutesTillSuspendLocKey = "IgorZ.SmartPower.Common.MinutesTillSuspend";

  void UpdateConsumerBuildingText() {
    if (_powerInputLimiter.IsSuspended) {
      _suspendReasonLabel.text = _powerInputLimiter.LowBatteriesCharge
          ? _uiFactory.Loc.T(LowBatteriesChargeLocKey)
          : _uiFactory.Loc.T(NotEnoughPowerLocKey);
      _suspendReasonLabel.ToggleDisplayStyle(visible: true);
      if (_powerInputLimiter.MinutesTillResume > 0) {
        _stateProgressLabel.text = _uiFactory.Loc.T(MinutesTillResumeLocKey, _powerInputLimiter.MinutesTillResume);
        _stateProgressLabel.ToggleDisplayStyle(visible: true);
      } else {
        _stateProgressLabel.ToggleDisplayStyle(visible: false);
      }
      return;
    }

    _suspendReasonLabel.ToggleDisplayStyle(visible: false);
    if (_powerInputLimiter.MinutesTillSuspend > 0) {
      _stateProgressLabel.text = _uiFactory.Loc.T(MinutesTillSuspendLocKey, _powerInputLimiter.MinutesTillSuspend);
      _stateProgressLabel.ToggleDisplayStyle(visible: true);
    } else {
      _stateProgressLabel.ToggleDisplayStyle(visible: false);
    }
  }
}
