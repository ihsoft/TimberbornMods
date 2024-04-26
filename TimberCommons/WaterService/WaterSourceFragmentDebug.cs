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
  Toggle _useMultiThreadSimulationToggle;
  Toggle _useGpuForContaminationSimulator;
  Toggle _useGpuForMoistureSimulation;
  
  public WaterSourceFragmentDebug(UIBuilder builder) {
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

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_useMultiThreadSimulationToggle)
        .AddComponent(_useGpuForContaminationSimulator)
        .AddComponent(_useGpuForMoistureSimulation)
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
