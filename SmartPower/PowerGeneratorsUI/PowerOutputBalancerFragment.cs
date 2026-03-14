// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.SmartPower.PowerGenerators;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.SmartPower.PowerGeneratorsUI;

sealed class PowerOutputBalancerFragment : IEntityPanelFragment {
  const string ChargeLevelLocKey = "IgorZ.SmartPower.PowerOutputBalancer.ChargeBatteriesRangeText";
  const string ApplyToAllGeneratorsLocKey = "IgorZ.SmartPower.PowerOutputBalancer.ApplyToAllGenerators";
  const string AppliedToGeneratorsLocKey = "IgorZ.SmartPower.PowerOutputBalancer.AppliedToGenerators";
  const string MinutesTillResumeLocKey = "IgorZ.SmartPower.Common.MinutesTillResume";
  const string MinutesTillSuspendLocKey = "IgorZ.SmartPower.Common.MinutesTillSuspend";

  readonly UiFactory _uiFactory;

  Label _suspendStatusLabel;  // It is patched in the stock UI.
  PanelFragmentPatcher _panelFragmentPatcher;

  VisualElement _root;
  Toggle _automateCheckbox;
  Label _chargeBatteriesText;
  MinMaxSlider _chargeBatteriesSlider;
  Button _applyToAllGeneratorsButton;

  PowerOutputBalancer _balancer;
  TimedUpdater _applyToAllUpdater;

  PowerOutputBalancerFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  public VisualElement InitializeFragment() {
    _root = _uiFactory.LoadVisualTreeAsset("IgorZ/PowerOutputBalancerFragment");
    _automateCheckbox = _root.Q<Toggle>("AutomateCheckbox");
    _automateCheckbox.RegisterValueChangedCallback(
        e => {
          _balancer.Automate = e.newValue;
          _balancer.UpdateState();
          UpdateControls();
        });
    _chargeBatteriesText = _root.Q<Label>("ChargeBatteriesText");
    _chargeBatteriesSlider = _root.Q<MinMaxSlider>("ChargeBatteriesSlider");
    _uiFactory.AddFixedStepChangeHandler(
        _chargeBatteriesSlider, 0.05f, 0.10f,
        newValue => {
          _balancer.DischargeBatteriesThreshold = newValue.x;
          _balancer.ChargeBatteriesThreshold = newValue.y;
          _balancer.UpdateState();
          UpdateControls();
        });
    _applyToAllGeneratorsButton = _root.Q<Button>("ApplyToAllGeneratorsButton");
    _applyToAllGeneratorsButton.clicked += ApplyToAllGenerators;

    _suspendStatusLabel = _uiFactory.CreateLabel();
    _suspendStatusLabel.ToggleDisplayStyle(visible: false);
    _panelFragmentPatcher = new PanelFragmentPatcher(
        _suspendStatusLabel, _root, PanelFragmentPatcher.MechanicalNodeFragmentName, "Generator");

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _balancer = entity.GetComponent<PowerOutputBalancer>();
    if (!_balancer) {
      return;
    }
    _panelFragmentPatcher.Patch();
    _automateCheckbox.SetValueWithoutNotify(_balancer.Automate);
    _chargeBatteriesSlider.SetValueWithoutNotify(
        new Vector2(_balancer.DischargeBatteriesThreshold, _balancer.ChargeBatteriesThreshold));
    UpdateControls();
    _root.ToggleDisplayStyle(visible: true);
    _applyToAllGeneratorsButton.ToggleDisplayStyle(visible: _balancer.Enabled);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _suspendStatusLabel.ToggleDisplayStyle(visible: false);
    _balancer = null;
  }

  public void UpdateFragment() {
    if (!_balancer) {
      return;
    }
    _applyToAllGeneratorsButton.ToggleDisplayStyle(visible: _balancer.Enabled);
    _applyToAllUpdater?.Update(
        () => {
          _applyToAllGeneratorsButton.text = _uiFactory.T(ApplyToAllGeneratorsLocKey);
          _applyToAllGeneratorsButton.SetEnabled(true);
          _applyToAllUpdater = null;
        });
    UpdateGeneratorBuildingText();
  }

  void UpdateControls() {
    _chargeBatteriesText.text = _uiFactory.T(
        ChargeLevelLocKey, Mathf.RoundToInt(_balancer.DischargeBatteriesThreshold * 100),
        Mathf.RoundToInt(_balancer.ChargeBatteriesThreshold * 100));
  }

  void ApplyToAllGenerators() {
    var affectedGenerators = 0;
    foreach (var balancer in _balancer.AllBalancers.Where(x => x != _balancer)) {
      affectedGenerators++;
      balancer.DuplicateFrom(_balancer);
    }
    _applyToAllUpdater = new TimedUpdater(1.0f, startNow: true);
    _applyToAllGeneratorsButton.text = _uiFactory.T(AppliedToGeneratorsLocKey, affectedGenerators);
    _applyToAllGeneratorsButton.SetEnabled(false);
  }

  void UpdateGeneratorBuildingText() {
    if (_balancer.MinutesTillResume > 0) {
      _suspendStatusLabel.text = _uiFactory.T(MinutesTillResumeLocKey, _balancer.MinutesTillResume);
      _suspendStatusLabel.ToggleDisplayStyle(visible: true);
    } else if (_balancer.MinutesTillSuspend > 0) {
      _suspendStatusLabel.text = _uiFactory.T(MinutesTillSuspendLocKey, _balancer.MinutesTillSuspend);
      _suspendStatusLabel.ToggleDisplayStyle(visible: true);
    } else {
      _suspendStatusLabel.ToggleDisplayStyle(visible: false);
    }
  }
}
