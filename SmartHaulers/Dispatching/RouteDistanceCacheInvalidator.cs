// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Navigation;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class RouteDistanceCacheInvalidator(
    DispatchCenterRegistry dispatchCenterRegistry,
    DistrictMap districtMap) : ISingletonNavMeshListener {
  public void OnNavMeshUpdated(NavMeshUpdate navMeshUpdate) {
    if (!navMeshUpdate.UpdatedRoads) {
      return;
    }
    var matched = false;
    foreach (var dispatchCenter in dispatchCenterRegistry.DispatchCenters) {
      if (DistrictHasUpdatedRoad(dispatchCenter, navMeshUpdate)) {
        dispatchCenter.ClearRouteCache();
        matched = true;
      }
    }
    if (!matched) {
      // DistrictMap can lose the old owner for removed roads by the time ordinary listeners run.
      // Fall back to a global clear only for unmatched road updates; normal edits stay district-local.
      DebugEx.Warning(
          "SmartHaulers could not match road navmesh update to a district; clearing all route caches. "
          + "roadNodes={0}, bounds={1}.",
          navMeshUpdate.RoadNodeIds.Count, navMeshUpdate.Bounds);
      foreach (var dispatchCenter in dispatchCenterRegistry.DispatchCenters) {
        dispatchCenter.ClearRouteCache();
      }
    }
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
