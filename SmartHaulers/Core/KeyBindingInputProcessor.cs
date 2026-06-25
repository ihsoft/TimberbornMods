// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.InputSystem;
using Timberborn.SingletonSystem;

namespace IgorZ.SmartHaulers.Core;

sealed class KeyBindingInputProcessor(InputService inputService) : IPostLoadableSingleton, IInputProcessor {
  internal const string ToggleDiagnosticsBindingKey = "IgorZ-SmartHaulersToggleDiagnostics";
  internal const string LogSnapshotBindingKey = "IgorZ-SmartHaulersLogSnapshot";
  internal const string ToggleDispatchPanelBindingKey = "IgorZ-SmartHaulersToggleDispatchPanel";
  internal const string CycleDispatchViewBindingKey = "IgorZ-SmartHaulersCycleDispatchView";

  public void PostLoad() {
    SmartHaulersState.Reset();
    inputService.AddInputProcessor(this);
  }

  public bool ProcessInput() {
    if (inputService.IsKeyDown(ToggleDiagnosticsBindingKey)) {
      SmartHaulersState.ToggleDiagnostics();
      return false;
    }
    if (inputService.IsKeyDown(LogSnapshotBindingKey)) {
      SmartHaulersState.RequestLogSnapshot();
      return false;
    }
    if (inputService.IsKeyDown(ToggleDispatchPanelBindingKey)) {
      SmartHaulersState.ToggleDispatchPanel();
      return false;
    }
    if (inputService.IsKeyDown(CycleDispatchViewBindingKey)) {
      SmartHaulersState.CycleDispatchViewMode();
    }
    return false;
  }
}
