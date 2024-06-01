// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.UI;
using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.GpuSimulators {

// ReSharper disable once ClassNeverInstantiated.Global
sealed class DebugUiFragment : IEntityPanelFragment {
  readonly UIBuilder _builder;
  
  VisualElement _root;
  Toggle _gpuSimulator;
  
  public DebugUiFragment(UIBuilder builder) {
    _builder = builder;
  }

  public VisualElement InitializeFragment() {
    _gpuSimulator = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Simulate physics on GPU", color: UiFactory.PanelNormalColor);
    _gpuSimulator.RegisterValueChangedCallback(
        _ => {
          GpuSimulatorsController.Self.EnableSimulator(_gpuSimulator.value);
        });

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_gpuSimulator)
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
