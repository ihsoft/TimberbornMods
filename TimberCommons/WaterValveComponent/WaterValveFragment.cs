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
  const string WaterFlowTextLocKey = "Limit water flow to: {0}";

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
        .AddComponent(_useCustomSimulationCheckbox)
        .AddComponent(_logStatsCheckbox)
        .AddComponent(_waterFlowText)
        .AddComponent(_waterFlowSlider)
        .AddComponent(_infoLabel)
        .BuildAndInitialize();;
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _waterValve = entity.GetComponentFast<WaterValve>();
    if (_waterValve != null) {
      _waterFlowSlider.SetValueWithoutNotify(_waterValve.FlowLimitSetting);
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
    //FIXME cms and loc string
    _waterFlowText.text = string.Format(WaterFlowTextLocKey, _waterValve.FlowLimitSetting);
    _waterFlowSlider.ToggleDisplayStyle(visible: adjustWaterFlow);
    if (_devModeManager.Enabled) {
      _waterFlowSlider.lowValue = 0;
      _waterFlowSlider.highValue = 10;
    } else {
      _waterFlowSlider.lowValue = _waterValve.MinimumInGameFlow;
      _waterFlowSlider.highValue = _waterValve.FlowLimit;
    }
    if (_devModeManager.Enabled) {
      var info = new List<string>();
      info.Add(string.Format("Water level at input: {0:0.00} m", _waterValve.WaterHeightAtInput));
      info.Add(string.Format("Water level at output: {0:0.00} m", _waterValve.WaterHeightAtOutput));
      info.Add(string.Format("Water flow: {0:0.00} cms", _waterValve.CurrentFlow));
      _infoLabel.text = string.Join("\n", info);
      _infoLabel.ToggleDisplayStyle(visible: true);
    } else {
      _infoLabel.ToggleDisplayStyle(visible: false);
    }
    _root.ToggleDisplayStyle(visible: _devModeManager.Enabled || adjustWaterFlow);
  }
}

}
