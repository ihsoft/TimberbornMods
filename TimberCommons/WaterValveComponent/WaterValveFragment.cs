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
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.WaterValveComponent {

sealed class WaterValveFragment : IEntityPanelFragment {
  const string WaterFlowTextLocKey = "Limit water flow to: {0} m³/s";
  const string WaterFlowLocKey = "Water flow: {0} m³/s";
  const string WaterDepthAtIntakeLocKey = "Water depth at intake: {0}";
  const string WaterDepthAtOuttakeLocKey = "Water depth at outtake: {0}";

  const string WaterLevelAtInputText = "Water level at input: {0:0.00}";
  const string WaterLevelAtOutputText = "Water level at output: {0:0.00}";
  const string FreeFlowText = "Free flow";
  const string LogExtraStatsText = "Log extra stats";

  readonly UIBuilder _builder;
  readonly ILoc _loc;
  readonly VisualElementLoader _visualElementLoader;
  readonly DevModeManager _devModeManager;

  VisualElement _root;
  Label _infoLabel;
  Label _waterFlowText;
  Slider _waterFlowSlider;
  Toggle _logStatsCheckbox;
  Toggle _freeFlowCheckbox;

  WaterValve _waterValve;

  public WaterValveFragment(UIBuilder builder, ILoc loc, VisualElementLoader visualElementLoader,
                            DevModeManager devModeManager) {
    _builder = builder;
    _loc = loc;
    _visualElementLoader = visualElementLoader;
    _devModeManager = devModeManager;
  }

  public VisualElement InitializeFragment() {
    var presets = _builder.Presets();
    _infoLabel = presets.Labels().Label(color: UiFactory.PanelNormalColor);
    _waterFlowText = presets.Labels().Label(color: UiFactory.PanelNormalColor);

    _logStatsCheckbox = _builder.Presets().Toggles()
         .CheckmarkInverted(text: LogExtraStatsText, color: UiFactory.PanelNormalColor);
    _logStatsCheckbox.RegisterValueChangedCallback(_ => _waterValve._logExtraStats = _logStatsCheckbox.value);
    _freeFlowCheckbox = _builder.Presets().Toggles()
        .CheckmarkInverted(text: FreeFlowText, color: UiFactory.PanelNormalColor);
    _freeFlowCheckbox.RegisterValueChangedCallback(
        _ => _waterValve._freeFlow = _freeFlowCheckbox.value);

    _waterFlowSlider = UiFactory.Create(_visualElementLoader, v => _waterValve.WaterFlow = v);

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_waterFlowText)
        .AddComponent(_waterFlowSlider)
        .AddComponent(_infoLabel)
        .AddComponent(_freeFlowCheckbox)
        .AddComponent(_logStatsCheckbox)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _waterValve = entity.GetComponentFast<WaterValve>();
    if (_waterValve != null && !_waterValve.ShowUIPanel) {
      _waterValve = null;
    } 
    if (_waterValve != null) {
      _freeFlowCheckbox.SetValueWithoutNotify(_waterValve._freeFlow);
      _logStatsCheckbox.SetValueWithoutNotify(_waterValve._logExtraStats);
      _waterFlowSlider.highValue = Mathf.Max(_waterValve.FlowLimit, _waterValve.WaterFlow);
      _waterFlowSlider.SetValueWithoutNotify(_waterValve.WaterFlow);
      _root.ToggleDisplayStyle(visible: true);
    }
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _waterValve = null;
  }

  public void UpdateFragment() {
    if (_waterValve == null) {
      _root.ToggleDisplayStyle(visible: false);
      return;
    }
    _freeFlowCheckbox.ToggleDisplayStyle(visible: _devModeManager.Enabled);
    _logStatsCheckbox.ToggleDisplayStyle(visible: _devModeManager.Enabled);
    var adjustWaterFlow = _devModeManager.Enabled || _waterValve.CanChangeFlowInGame;
    _waterFlowText.ToggleDisplayStyle(visible: adjustWaterFlow);
    _waterFlowText.text = string.Format(WaterFlowTextLocKey, _waterValve.WaterFlow.ToString("0.0#"));
    _waterFlowSlider.ToggleDisplayStyle(visible: adjustWaterFlow);
    if (_devModeManager.Enabled) {
      _waterFlowSlider.lowValue = 0;
      _waterFlowSlider.highValue = 10;
    } else {
      _waterFlowSlider.lowValue = Mathf.Min(_waterValve.MinimumInGameFlow, _waterValve.WaterFlow);
      _waterFlowSlider.highValue = Mathf.Max(_waterValve.FlowLimit, _waterValve.WaterFlow);
    }
    var info = new List<string> {
        _loc.T(WaterDepthAtIntakeLocKey, _waterValve.WaterDepthAtIntake.ToString("0.00")),
        _loc.T(WaterDepthAtOuttakeLocKey, _waterValve.WaterDepthAtOuttake.ToString("0.00")),
        _loc.T(WaterFlowLocKey, _waterValve.CurrentFlow.ToString("0.0"))
    };
    if (_devModeManager.Enabled) {
      info.Add("\nDEV MODE DATA:");
      info.Add(string.Format(WaterLevelAtInputText, _waterValve.WaterHeightAtInput));
      info.Add(string.Format(WaterLevelAtOutputText, _waterValve.WaterHeightAtOutput));
    }
    _infoLabel.text = string.Join("\n", info);
  }
}

}
