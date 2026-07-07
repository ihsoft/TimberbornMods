// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Navigation;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class RouteDistanceCacheInvalidator(
    DispatchCenterRegistry dispatchCenterRegistry,
    DistrictMap districtMap) : ISingletonNavMeshListener {
  public void OnNavMeshUpdated(NavMeshUpdate navMeshUpdate) {
    if (!navMeshUpdate.UpdatedRoads) {
      return;
    }
    foreach (var dispatchCenter in dispatchCenterRegistry.DispatchCenters) {
      if (DistrictHasUpdatedRoad(dispatchCenter, navMeshUpdate)) {
        dispatchCenter.ClearRouteCache();
      }
    }
    // Orphan roads outside every district do not participate in district-local routing. Vanilla DistrictMap ignores
    // those road updates too, so they should not invalidate SmartHaulers route caches or spam diagnostics logs.
  }

  bool DistrictHasUpdatedRoad(HaulerDispatchCenter dispatchCenter, NavMeshUpdate navMeshUpdate) {
    var district = dispatchCenter.DistrictCenter?.District;
    if (district == null) {
      return false;
    }
    foreach (var nodeId in navMeshUpdate.RoadNodeIds) {
      if (districtMap.RoadNodeIsOccupiedByDistrict(district, nodeId)) {
        return true;
      }
    }
    return false;
  }
}
