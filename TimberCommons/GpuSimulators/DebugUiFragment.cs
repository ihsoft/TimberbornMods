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
  Toggle _useGpuForContaminationSimulator;
  Toggle _useGpuForMoistureSimulation;
  Toggle _useGpuForWaterSimulation;
  
  public DebugUiFragment(UIBuilder builder) {
    _builder = builder;
  }

  public VisualElement InitializeFragment() {
    _useGpuForContaminationSimulator = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Simulate soil contamination on GPU", color: UiFactory.PanelNormalColor);
    _useGpuForContaminationSimulator.RegisterValueChangedCallback(
        _ => {
          GpuSimulatorsController.Self.EnableSoilContaminationSim(_useGpuForContaminationSimulator.value);
        });
    _useGpuForMoistureSimulation = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Simulate soil moisture on GPU", color: UiFactory.PanelNormalColor);
    _useGpuForMoistureSimulation.RegisterValueChangedCallback(
        _ => {
          GpuSimulatorsController.Self.EnableSoilMoistureSim(_useGpuForMoistureSimulation.value);
        });
    _useGpuForWaterSimulation = _builder.Presets().Toggles()
        .CheckmarkInverted(text: "Water simulation on GPU", color: UiFactory.PanelNormalColor);
    _useGpuForWaterSimulation.RegisterValueChangedCallback(
        _ => {
          GpuSimulatorsController.Self.EnableWaterSimulator(_useGpuForWaterSimulation.value);
        });

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_useGpuForContaminationSimulator)
        .AddComponent(_useGpuForMoistureSimulation)
        .AddComponent(_useGpuForWaterSimulation)
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
