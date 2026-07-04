// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.InventorySystem;
using Timberborn.Navigation;
using UnityEngine;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class TransportDistanceEstimator(DispatchPerformanceStats performanceStats) {
  public bool TryGetRouteDistance(Inventory source, Inventory target, out float distance) {
    var start = DispatchPerformanceStats.Timestamp();
    try {
      return TryGetRouteDistanceInternal(source, target, out distance);
    } finally {
      performanceStats.EndDecisionRoutePath(start);
    }
  }

  public bool TryGetDistanceToInventory(Inventory inventory, Vector3 position, out float distance) {
    var start = DispatchPerformanceStats.Timestamp();
    try {
      return TryGetDistanceToInventoryInternal(inventory, position, out distance);
    } finally {
      performanceStats.EndDecisionPickupPath(start);
    }
  }

  static bool TryGetRouteDistanceInternal(Inventory source, Inventory target, out float distance) {
    var sourceAccessible = source ? source.GetEnabledComponent<Accessible>() : null;
    var targetAccessible = target ? target.GetEnabledComponent<Accessible>() : null;
    if (sourceAccessible
        && targetAccessible
        && TransportPathDistance.TryFindRoadPath(sourceAccessible, targetAccessible, out distance)) {
      return true;
    }
    distance = float.NaN;
    return false;
  }

  static bool TryGetDistanceToInventoryInternal(Inventory inventory, Vector3 position, out float distance) {
    var accessible = inventory ? inventory.GetEnabledComponent<Accessible>() : null;
    if (accessible && accessible.FindPathUnlimitedRange(position, [], out distance)) {
      return true;
    }
    distance = float.NaN;
    return false;
  }
}
