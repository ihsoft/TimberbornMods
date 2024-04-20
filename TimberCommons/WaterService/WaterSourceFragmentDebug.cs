// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberCommons.GpuSimulators;
using IgorZ.TimberDev.UI;
using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.WaterService {

sealed class WaterSourceFragmentDebug : IEntityPanelFragment {
  readonly UIBuilder _builder;
  
  VisualElement _root;
  Toggle _usePatchedSimulationToggle;
  Toggle _useGPUSimulationToggle;

  public WaterSourceFragmentDebug(UIBuilder builder) {
    _builder = builder;
  }

  public VisualElement InitializeFragment() {
    _usePatchedSimulationToggle = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Patched water simulation", color: UiFactory.PanelNormalColor);
    _usePatchedSimulationToggle.RegisterValueChangedCallback(
        _ => {
          ParallelWaterSimulatorPatch.UsePatchedSimulator = _usePatchedSimulationToggle.value;
          ParallelSoilMoistureSimulatorPatch.UsePatchedSimulator = _usePatchedSimulationToggle.value;
          ParallelSoilContaminationSimulatorPatch.UsePatchedSimulator = _usePatchedSimulationToggle.value;
        });
    _useGPUSimulationToggle = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Use GPU simulation", color: UiFactory.PanelNormalColor);
    _useGPUSimulationToggle.RegisterValueChangedCallback(
        _ => {
          GpuSoilContaminationSimulator.Self.IsEnabled = _useGPUSimulationToggle.value;
        });

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_usePatchedSimulationToggle)
        .AddComponent(_useGPUSimulationToggle)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _root.ToggleDisplayStyle(visible: true);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
  }

  public void UpdateFragment() {
  }
}
}
