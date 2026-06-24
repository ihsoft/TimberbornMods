// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.SmartHaulers.Core;

static class SmartHaulersState {
  public static bool DiagnosticsEnabled { get; private set; }
  public static bool LogSnapshotRequested { get; private set; }

  public static void ToggleDiagnostics() {
    DiagnosticsEnabled = !DiagnosticsEnabled;
  }

  public static void RequestLogSnapshot() {
    LogSnapshotRequested = true;
  }

  public static bool ConsumeLogSnapshotRequest() {
    if (!LogSnapshotRequested) {
      return false;
    }
    LogSnapshotRequested = false;
    return true;
  }
}
