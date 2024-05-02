// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.UI;
using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.MultiThreadSimulators {

// ReSharper disable once ClassNeverInstantiated.Global
sealed class DebugUiFragment : IEntityPanelFragment {
  readonly UIBuilder _builder;
  
  VisualElement _root;
  Toggle _useMultiThreadSimulationToggle;
  
  public DebugUiFragment(UIBuilder builder) {
    _builder = builder;
  }

  public VisualElement InitializeFragment() {
    _useMultiThreadSimulationToggle = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Multi-thread water simulation", color: UiFactory.PanelNormalColor);
    _useMultiThreadSimulationToggle.RegisterValueChangedCallback(
        _ => {
          ParallelWaterSimulatorPatch.UsePatchedSimulator = _useMultiThreadSimulationToggle.value;
          ParallelSoilMoistureSimulatorPatch.UsePatchedSimulator = _useMultiThreadSimulationToggle.value;
          ParallelSoilContaminationSimulatorPatch.UsePatchedSimulator = _useMultiThreadSimulationToggle.value;
        });

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_useMultiThreadSimulationToggle)
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
