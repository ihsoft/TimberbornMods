// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
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
  const string WaterFlowTextLocKey = "Limit water flow to: {0} cms";
  const string WaterFlowLocKey = "Water flow: {0} cms";
  const string WaterLevelAtInput = "Water level at input: {0:0.00}";
  const string WaterLevelAtOutput = "Water level at output: {0:0.00}";

  readonly UIBuilder _builder;
  readonly ILoc _loc;
  readonly VisualElementLoader _visualElementLoader;
  readonly DevModeManager _devModeManager;

  static readonly Color NormalColor = new(0.8f, 0.8f, 0.8f);

  VisualElement _root;
  Label _infoLabel;
  Label _waterFlowText;
  Slider _waterFlowSlider;
  Toggle _logStatsCheckbox;
  Toggle _useCustomSimulationCheckbox;

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
    _infoLabel = presets.Labels().Label(color: NormalColor);
    _waterFlowText = presets.Labels().Label(color: NormalColor);

    _logStatsCheckbox = _builder.Presets().Toggles()
         .CheckmarkInverted(text: "Log extra stats", color: NormalColor);
    _logStatsCheckbox.RegisterValueChangedCallback(_ => _waterValve._logExtraStats = _logStatsCheckbox.value);
    _useCustomSimulationCheckbox = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Use custom simulation", color: NormalColor);
    _useCustomSimulationCheckbox.RegisterValueChangedCallback(
        _ => _waterValve._useCustomSimulation = _useCustomSimulationCheckbox.value);

    _waterFlowSlider = _visualElementLoader.LoadVisualElement("Common/IntegerSlider").Q<Slider>("Slider");
    _waterFlowSlider.RegisterValueChangedCallback(
        _ => {
          var value = Mathf.Round(_waterFlowSlider.value / 0.05f) * 0.05f;
          _waterFlowSlider.SetValueWithoutNotify(value);
          _waterValve.FlowLimitSetting = value;
        });

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_waterFlowText)
        .AddComponent(_waterFlowSlider)
        .AddComponent(_infoLabel)
        .AddComponent(_useCustomSimulationCheckbox)
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
      _useCustomSimulationCheckbox.SetValueWithoutNotify(_waterValve._useCustomSimulation);
      _logStatsCheckbox.SetValueWithoutNotify(_waterValve._logExtraStats);
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
    _useCustomSimulationCheckbox.ToggleDisplayStyle(visible: _devModeManager.Enabled);
    _logStatsCheckbox.ToggleDisplayStyle(visible: _devModeManager.Enabled);
    var adjustWaterFlow = _devModeManager.Enabled || _waterValve.CanChangeFlowInGame;
    _waterFlowText.ToggleDisplayStyle(visible: adjustWaterFlow);
    _waterFlowText.text = string.Format(WaterFlowTextLocKey, _waterValve.FlowLimitSetting.ToString("0.0#"));
    _waterFlowSlider.ToggleDisplayStyle(visible: adjustWaterFlow);
    if (_devModeManager.Enabled) {
      _waterFlowSlider.lowValue = 0;
      _waterFlowSlider.highValue = 10;
    } else {
      _waterFlowSlider.lowValue = Mathf.Min(_waterValve.MinimumInGameFlow, _waterValve.FlowLimitSetting);
      _waterFlowSlider.highValue = Mathf.Max(_waterValve.FlowLimit, _waterValve.FlowLimitSetting);
    }
    _waterFlowSlider.SetValueWithoutNotify(_waterValve.FlowLimitSetting);
    var info = new List<string> {
        _loc.T(WaterFlowLocKey, _waterValve.CurrentFlow.ToString("0.0"))
    };
    if (_devModeManager.Enabled) {
      info.Add("\nDEV MODE DATA:");
      info.Add(string.Format(WaterLevelAtInput, _waterValve.WaterHeightAtInput));
      info.Add(string.Format(WaterLevelAtOutput, _waterValve.WaterHeightAtOutput));
    }
    _infoLabel.text = string.Join("\n", info);
  }
}

}
