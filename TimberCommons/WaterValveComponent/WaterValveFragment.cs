// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.WaterValveComponent {

sealed class WaterValveFragment : IEntityPanelFragment {
  readonly UIBuilder _builder;
  readonly ILoc _loc;
  readonly VisualElementLoader _visualElementLoader;

  VisualElement _root;
  Label _infoLabel;
  Slider _waterFlowSlider;
  Toggle _logStatsCheckbox;
  Toggle _useCustomSimulationCheckbox;

  WaterValve _waterValve;

  public WaterValveFragment(UIBuilder builder, ILoc loc, VisualElementLoader visualElementLoader) {
    _builder = builder;
    _loc = loc;
    _visualElementLoader = visualElementLoader;
  }

  public VisualElement InitializeFragment() {
    var presets = _builder.Presets();
    _infoLabel = presets.Labels().Label(color: new Color(0.8f, 0.8f, 0.8f));

    _logStatsCheckbox = _builder.Presets().Toggles()
         .CheckmarkInverted(text: "Log stats", color: new Color(0.8f, 0.8f, 0.8f));
    _logStatsCheckbox.RegisterValueChangedCallback(_ => _waterValve._logStats = _logStatsCheckbox.value);
    _useCustomSimulationCheckbox = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Use custom simulation", color: new Color(0.8f, 0.8f, 0.8f));
    _useCustomSimulationCheckbox.RegisterValueChangedCallback(
        _ => _waterValve._useCustomSimulation = _useCustomSimulationCheckbox.value);

    _waterFlowSlider = _visualElementLoader.LoadVisualElement("Common/IntegerSlider").Q<Slider>("Slider");
    _waterFlowSlider.lowValue = 0.0f;
    _waterFlowSlider.highValue = 10.0f;
    _waterFlowSlider.RegisterValueChangedCallback(
        _ => {
          var value = Mathf.Round(_waterFlowSlider.value / 0.05f) * 0.05f;
          _waterFlowSlider.SetValueWithoutNotify(value);
          _waterValve._waterFlowPerSecond = value;
        });
    
    var uIFragmentBuilder = _builder.CreateFragmentBuilder()
        .AddComponent(_useCustomSimulationCheckbox)
        .AddComponent(_logStatsCheckbox)
        .AddComponent(_waterFlowSlider)
        .AddComponent(_infoLabel);
    _root = uIFragmentBuilder.BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _waterValve = entity.GetComponentFast<WaterValve>();
    if (_waterValve != null) {
      _waterFlowSlider.value = _waterValve._waterFlowPerSecond;
      _useCustomSimulationCheckbox.value = _waterValve._useCustomSimulation;
      _logStatsCheckbox.value = _waterValve._logStats;
    }
    _root.ToggleDisplayStyle(visible: _waterValve != null);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _waterValve = null;
  }

  public void UpdateFragment() {
    if (_waterValve == null) {
      return;
    }
    var info = new List<string>();
    info.Add(string.Format("Water level at input: {0:0.00} m", _waterValve.WaterHeightAtInput));
    info.Add(string.Format("Water level at output: {0:0.00} m", _waterValve.WaterHeightAtOutput));
    info.Add(string.Format("Water flow: {0:0.00} cms", _waterValve.CurrentFlow));
    info.Add(string.Format("Flow limit: {0:0.00} cms", _waterValve.FlowLimit));
    _infoLabel.text = string.Join("\n", info);
  }
}

}
