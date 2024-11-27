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
  const string AutomateLocKey = "IgorZ.SmartPower.PowerOutputBalancer.AutomateGenerator";
  const string ChargeLevelLocKey = "IgorZ.SmartPower.PowerOutputBalancer.ChargeBatteriesRangeText";
  const string ApplyToAllGeneratorsLocKey = "IgorZ.SmartPower.PowerOutputBalancer.ApplyToAllGenerators";
  const string AppliedToGeneratorsLocKey = "IgorZ.SmartPower.PowerOutputBalancer.AppliedToGenerators";

  readonly UiFactory _uiFactory;

  Label _suspendStatusLabel;  // It is patched in the stock UI.
  PanelFragmentPatcher<Label> _panelFragmentPatcher;

  VisualElement _root;
  Toggle _automateCheckbox;
  Label _chargeBatteriesText;
  MinMaxSlider2 _chargeBatteriesSlider;
  Button _applyToAllGeneratorsButton;

  PowerOutputBalancer _balancer;
  TimedUpdater _applyToAllUpdater;

  PowerOutputBalancerFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  public VisualElement InitializeFragment() {
    _automateCheckbox = _uiFactory.CreateToggle(
        AutomateLocKey, _ => {
          _balancer.Automate = _automateCheckbox.value;
          _balancer.UpdateState();
          UpdateControls();
        });

    _chargeBatteriesSlider = _uiFactory.CreateMinMaxSlider(
        _ => {
          _balancer.DischargeBatteriesThreshold = _chargeBatteriesSlider.value.x;
          _balancer.ChargeBatteriesThreshold = _chargeBatteriesSlider.value.y;
          _balancer.UpdateState();
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

    _suspendStatusLabel = _uiFactory.CreateLabel();
    _suspendStatusLabel.ToggleDisplayStyle(visible: false);
    _panelFragmentPatcher = new PanelFragmentPatcher<Label>(
        _suspendStatusLabel, _root, PanelFragmentNames.MechanicalNodeFragmentName, "Generator");

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _balancer = entity.GetComponentFast<PowerOutputBalancer>();
    if (!_balancer) {
      return;
    }
    _panelFragmentPatcher.Patch();
    _automateCheckbox.SetValueWithoutNotify(_balancer.Automate);
    _chargeBatteriesSlider.SetValueWithoutNotify(
        new Vector2(_balancer.DischargeBatteriesThreshold, _balancer.ChargeBatteriesThreshold));
    UpdateControls();
    _root.ToggleDisplayStyle(visible: true);
    _applyToAllGeneratorsButton.ToggleDisplayStyle(visible: _balancer.enabled);
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
    _applyToAllGeneratorsButton.ToggleDisplayStyle(visible: _balancer.enabled);
    _applyToAllUpdater?.Update(
        () => {
          _applyToAllGeneratorsButton.text = _uiFactory.Loc.T(ApplyToAllGeneratorsLocKey);
          _applyToAllGeneratorsButton.SetEnabled(true);
          _applyToAllUpdater = null;
        });
    UpdateGeneratorBuildingText();
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
      balancer.UpdateState();
    }
    _applyToAllUpdater = new TimedUpdater(1.0f, startNow: true);
    _applyToAllGeneratorsButton.text = _uiFactory.Loc.T(AppliedToGeneratorsLocKey, affectedGenerators);
    _applyToAllGeneratorsButton.SetEnabled(false);
  }

  const string MinutesTillResumeLocKey = "IgorZ.SmartPower.Common.MinutesTillResume";
  const string MinutesTillSuspendLocKey = "IgorZ.SmartPower.Common.MinutesTillSuspend";

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
