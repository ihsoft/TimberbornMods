// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.SmartHaulers.Core;
using Timberborn.TickSystem;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class HaulerDispatchRefreshService(
    DispatchCenterRegistry dispatchCenterRegistry,
    DispatchPerformanceStats performanceStats) : ITickableSingleton {
  public void Tick() {
    if (!SmartHaulersState.DiagnosticsEnabled && !SmartHaulersState.LogSnapshotRequested) {
      return;
    }
    RefreshSnapshots();
  }

  public void RefreshSnapshots() {
    performanceStats.BeginRefresh();
    try {
      foreach (var dispatchCenter in dispatchCenterRegistry.DispatchCenters) {
        dispatchCenter.RefreshSnapshot();
      }
    } finally {
      performanceStats.EndRefresh();
    }
  }
}
