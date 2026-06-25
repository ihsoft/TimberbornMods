// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.InventorySystem;
using Timberborn.Navigation;
using UnityEngine;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class TransportDistanceEstimator {
  public bool TryGetRouteDistance(Inventory source, Inventory target, out float distance) {
    var sourceAccessible = source ? source.GetEnabledComponent<Accessible>() : null;
    var targetAccessible = target ? target.GetEnabledComponent<Accessible>() : null;
    if (sourceAccessible
        && targetAccessible
        && sourceAccessible.HasSingleAccess
        && sourceAccessible.FindRoadPath(targetAccessible, out distance)) {
      return true;
    }
    distance = float.NaN;
    return false;
  }

  public bool TryGetDistanceToInventory(Inventory inventory, Vector3 position, out float distance) {
    var accessible = inventory ? inventory.GetEnabledComponent<Accessible>() : null;
    if (accessible && accessible.FindPathUnlimitedRange(position, [], out distance)) {
      return true;
    }
    distance = float.NaN;
    return false;
  }
}
