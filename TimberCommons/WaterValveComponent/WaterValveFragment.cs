// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.TimberDev.UI;
using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.Debugging;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.WaterValveComponent {

/// <summary>Shows UP panel for the WaterValve component.</summary>
sealed class WaterValveFragment : IEntityPanelFragment {
  const string WaterFlowLimitLocKey = "IgorZ.TimberCommons.WaterValve.WaterFlowLimit";
  const string CurrentWaterFlowLocKey = "IgorZ.TimberCommons.WaterValve.CurrentWaterFlow";
  const string WaterDepthAtIntakeLocKey = "IgorZ.TimberCommons.WaterValve.WaterDepthAtIntake";
  const string WaterDepthAtOuttakeLocKey = "IgorZ.TimberCommons.WaterValve.WaterDepthAtOuttake";

  const string WaterLevelAtInputText = "Water level at input: {0:0.00}";
  const string WaterLevelAtOutputText = "Water level at output: {0:0.00}";
  const string MinimumLevelAtIntakeText = "Minimum level at intake: {0:0.00}";
  const string MaximumLevelAtOuttakeText = "Maximum level at outtake: {0:0.00}";
  const string FreeFlowText = "Free flow";
  const string LogExtraStatsText = "Log extra stats";

  readonly UIBuilder _builder;
  readonly ILoc _loc;
  readonly VisualElementLoader _visualElementLoader;
  readonly DevModeManager _devModeManager;

  VisualElement _root;
  Label _infoLabel;
  Label _waterFlowLimitText;
  Slider _waterFlowLimitSlider;
  Label _inputWaterLevelText;
  Slider _inputWaterLevelSlider;
  Label _outputWaterLevelText;
  Slider _outputWaterLevelSlider;
  Toggle _logStatsCheckbox;
  Toggle _freeFlowCheckbox;

  WaterValve _waterValve;

  public WaterValveFragment(UIBuilder builder, ILoc loc, VisualElementLoader visualElementLoader,
                            DevModeManager devModeManager, EventBus eventBus) {
    _builder = builder;
    _loc = loc;
    _visualElementLoader = visualElementLoader;
    _devModeManager = devModeManager;
    eventBus.Register(this);
  }

  public VisualElement InitializeFragment() {
    var presets = _builder.Presets();
    _infoLabel = presets.Labels().Label(color: UiFactory.PanelNormalColor);

    _logStatsCheckbox = _builder.Presets().Toggles()
         .CheckmarkInverted(text: LogExtraStatsText, color: UiFactory.PanelNormalColor);
    _logStatsCheckbox.RegisterValueChangedCallback(_ => _waterValve.LogExtraStats = _logStatsCheckbox.value);

    _freeFlowCheckbox = _builder.Presets().Toggles()
        .CheckmarkInverted(text: FreeFlowText, color: UiFactory.PanelNormalColor);
    _freeFlowCheckbox.RegisterValueChangedCallback(
        _ => _waterValve.IsFreeFlow = _freeFlowCheckbox.value);

    _waterFlowLimitText = presets.Labels().Label(color: UiFactory.PanelNormalColor);
    _waterFlowLimitSlider = UiFactory.Create(_visualElementLoader, v => _waterValve.WaterFlow = v);

    _inputWaterLevelText = presets.Labels().Label(color: UiFactory.PanelNormalColor);
    _inputWaterLevelSlider = UiFactory.Create(
        _visualElementLoader, v => _waterValve.MinWaterLevelAtIntake = v, highValue: 5);

    _outputWaterLevelText = presets.Labels().Label(color: UiFactory.PanelNormalColor);
    _outputWaterLevelSlider = UiFactory.Create(
        _visualElementLoader, v => _waterValve.MaxWaterLevelAtOuttake = v, highValue: 5);

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_waterFlowLimitText).AddComponent(_waterFlowLimitSlider)
        .AddComponent(_infoLabel)
        .AddComponent(_inputWaterLevelText).AddComponent(_inputWaterLevelSlider)
        .AddComponent(_outputWaterLevelText).AddComponent(_outputWaterLevelSlider)
        .AddComponent(_freeFlowCheckbox)
        .AddComponent(_logStatsCheckbox)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);

    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _waterValve = entity.GetComponentFast<WaterValve>();
    if (_waterValve != null) {
      if (_devModeManager.Enabled) {
        _waterFlowLimitSlider.lowValue = 0;
        _waterFlowLimitSlider.highValue = Mathf.Max(_waterValve.WaterFlow, 10);
      } else if (_waterValve.CanChangeFlowInGame) {
        _waterFlowLimitSlider.lowValue = Mathf.Min(_waterValve.MinimumInGameFlow, _waterValve.WaterFlow);
        _waterFlowLimitSlider.highValue = Mathf.Max(_waterValve.FlowLimit, _waterValve.WaterFlow);
      }
      var showWaterFlowLimitControls = _devModeManager.Enabled || _waterValve.CanChangeFlowInGame;
      _waterFlowLimitText.ToggleDisplayStyle(visible: showWaterFlowLimitControls);
      _waterFlowLimitSlider.ToggleDisplayStyle(visible: showWaterFlowLimitControls);
      _waterFlowLimitSlider.SetValueWithoutNotify(_waterValve.WaterFlow);

      _inputWaterLevelText.ToggleDisplayStyle(visible: _devModeManager.Enabled);
      _inputWaterLevelSlider.SetValueWithoutNotify(_waterValve.MinWaterLevelAtIntake);
      _inputWaterLevelSlider.ToggleDisplayStyle(visible: _devModeManager.Enabled);

      _outputWaterLevelText.ToggleDisplayStyle(visible: _devModeManager.Enabled);
      _outputWaterLevelSlider.SetValueWithoutNotify(_waterValve.MaxWaterLevelAtOuttake);
      _outputWaterLevelSlider.ToggleDisplayStyle(visible: _devModeManager.Enabled);

      _freeFlowCheckbox.SetValueWithoutNotify(_waterValve.IsFreeFlow);
      _freeFlowCheckbox.ToggleDisplayStyle(visible: _devModeManager.Enabled);

      _logStatsCheckbox.SetValueWithoutNotify(_waterValve.LogExtraStats);
      _logStatsCheckbox.ToggleDisplayStyle(visible: _devModeManager.Enabled);
    }
    _root.ToggleDisplayStyle(visible: _waterValve != null && (_waterValve.ShowUIPanel || _devModeManager.Enabled));
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _waterValve = null;
  }

  public void UpdateFragment() {
    if (_waterValve == null || !_waterValve.ShowUIPanel && !_devModeManager.Enabled) {
      return;
    }
    _waterFlowLimitText.text = _loc.T(WaterFlowLimitLocKey, _waterValve.WaterFlow.ToString("0.0#"));
    if (_devModeManager.Enabled) {
      _inputWaterLevelText.text = string.Format(MinimumLevelAtIntakeText, _waterValve.MinWaterLevelAtIntake);
      _outputWaterLevelText.text = string.Format(MaximumLevelAtOuttakeText, _waterValve.MaxWaterLevelAtOuttake);
    }
    if (_waterValve.enabled) {
      var info = new List<string> {
          _loc.T(WaterDepthAtIntakeLocKey, _waterValve.WaterDepthAtIntake.ToString("0.00")),
          _loc.T(WaterDepthAtOuttakeLocKey, _waterValve.WaterDepthAtOuttake.ToString("0.00")),
          _loc.T(CurrentWaterFlowLocKey, _waterValve.CurrentFlow.ToString("0.0"))
      };
      if (_devModeManager.Enabled) {
        info.Add("\nDEV MODE DATA:");
        info.Add(string.Format(WaterLevelAtInputText, _waterValve.WaterHeightAtInput));
        info.Add(string.Format(WaterLevelAtOutputText, _waterValve.WaterHeightAtOutput));
      }
      _infoLabel.text = string.Join("\n", info);
    }
    _infoLabel.ToggleDisplayStyle(visible: _waterValve.enabled);
  }

  [OnEvent]
  public void OnDevModeToggledEvent(DevModeToggledEvent @event) {
    if (_waterValve != null) {
      ShowFragment(_waterValve);
    }
  }
}

}
