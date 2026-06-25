// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.SmartHaulers.Core;

static class SmartHaulersState {
  public static bool DiagnosticsEnabled { get; private set; }
  public static bool DispatchPanelVisible { get; private set; } = true;
  public static DispatchDebugViewMode DispatchViewMode { get; private set; } = DispatchDebugViewMode.All;
  public static bool LogSnapshotRequested { get; private set; }
  public static bool SnapshotRefreshRequested { get; private set; }

  public static void Reset() {
    DiagnosticsEnabled = false;
    DispatchPanelVisible = true;
    DispatchViewMode = DispatchDebugViewMode.All;
    LogSnapshotRequested = false;
    SnapshotRefreshRequested = false;
  }

  public static void ToggleDiagnostics() {
    DiagnosticsEnabled = !DiagnosticsEnabled;
    if (DiagnosticsEnabled) {
      SnapshotRefreshRequested = true;
    }
  }

  public static void ToggleDispatchPanel() {
    DispatchPanelVisible = !DispatchPanelVisible;
  }

  public static void CycleDispatchViewMode() {
    DispatchViewMode = DispatchViewMode switch {
        DispatchDebugViewMode.All => DispatchDebugViewMode.Agents,
        DispatchDebugViewMode.Agents => DispatchDebugViewMode.Orders,
        DispatchDebugViewMode.Orders => DispatchDebugViewMode.All,
        _ => DispatchDebugViewMode.All,
    };
  }

  public static void RequestLogSnapshot() {
    LogSnapshotRequested = true;
    SnapshotRefreshRequested = true;
  }

  public static bool ConsumeLogSnapshotRequest() {
    if (!LogSnapshotRequested) {
      return false;
    }
    LogSnapshotRequested = false;
    return true;
  }

  public static bool ConsumeSnapshotRefreshRequest() {
    if (!SnapshotRefreshRequested) {
      return false;
    }
    SnapshotRefreshRequested = false;
    return true;
  }
}
