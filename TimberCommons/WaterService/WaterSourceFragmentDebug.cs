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
  Toggle _useGPUSimulationToggle1;
  Toggle _useGPUSimulationToggle2;

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
    _useGPUSimulationToggle1 = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Use GPU simulation #1", color: UiFactory.PanelNormalColor);
    _useGPUSimulationToggle1.RegisterValueChangedCallback(
        _ => {
          GpuSoilContaminationSimulator.Self.IsEnabled = _useGPUSimulationToggle1.value;
          GpuSoilContaminationSimulator2.Self.IsEnabled = false;
          _useGPUSimulationToggle2.SetValueWithoutNotify(false);
        });
    _useGPUSimulationToggle2 = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Use GPU simulation #2", color: UiFactory.PanelNormalColor);
    _useGPUSimulationToggle2.RegisterValueChangedCallback(
        _ => {
          GpuSoilContaminationSimulator.Self.IsEnabled = false;
          _useGPUSimulationToggle1.SetValueWithoutNotify(false);
          GpuSoilContaminationSimulator2.Self.IsEnabled = _useGPUSimulationToggle2.value;
        });

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_usePatchedSimulationToggle)
        .AddComponent(_useGPUSimulationToggle1)
        .AddComponent(_useGPUSimulationToggle2)
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
