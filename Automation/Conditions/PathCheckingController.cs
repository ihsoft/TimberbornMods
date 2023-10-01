// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Automation.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlockSystemNavigation;
using Timberborn.Buildings;
using Timberborn.ConstructibleSystem;
using Timberborn.ConstructionSites;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Localization;
using Timberborn.Navigation;
using Timberborn.SingletonSystem;
using Timberborn.StatusSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace Automation.Conditions {

sealed class PathCheckingController : ITickableSingleton, ISingletonNavMeshListener {
  const float MaxCompletionProgress = 0.8f;
  const float MinDistanceFromConstructionSite = 3.0f;

  #region ITickableSingleton implementation
  public void Tick() {
    CheckBlockedAccess();
  }
  #endregion

  #region API
  /// <summary>Add the path checking condition to monitor.</summary>
  public void AddCondition(CheckAccessBlockCondition condition) {
    var site = condition.Behavior.GetComponentFast<ConstructionSite>();
    if (!_conditionsIndex.TryGetValue(site, out var valueList)) {
      valueList = new List<CheckAccessBlockCondition>();
      _conditionsIndex[site] = valueList;
    }
    valueList.Add(condition);
    MarkIndexesDirty();
  }

  /// <summary>Removes the path checking condition from monitor and resets all caches.</summary>
  public void RemoveCondition(CheckAccessBlockCondition condition) {
    var site = condition.Behavior.GetComponentFast<ConstructionSite>();
    var customStatus = condition.Behavior.GetComponentFast<UnreachableStatus>();
    if (customStatus != null) {
      customStatus.StatusToggle.Deactivate();
    }
    if (_conditionsIndex.TryGetValue(site, out var valueList)) {
      valueList.Remove(condition);
      if (valueList.Count == 0) {
        RemoveSite(site);
      } else {
        MarkIndexesDirty();
      }
    }
  }
  #endregion

  #region ISingletonNavMeshListener implemenation
  /// <inheritdoc/>
  public void OnNavMeshUpdated(NavMeshUpdate navMeshUpdate) {
    MarkIndexesDirty();
  }
  #endregion

  #region Implementation
  readonly DistrictCenterRegistry _districtCenterRegistry;
  readonly INavigationService _navigationService;
  readonly EntityComponentRegistry _entityComponentRegistry;
  readonly IDistrictService _districtService;
  readonly BaseInstantiator _baseInstantiator;
  readonly ILoc _loc;

  /// <summary>All path checking conditions on the site.</summary>
  readonly Dictionary<ConstructionSite, List<CheckAccessBlockCondition>> _conditionsIndex = new();

  /// <summary>
  /// The sites that are reachable with the unlimited range, but are too far from the closest road for the builders to
  /// reach it.
  /// </summary>
  readonly HashSet<ConstructionSite> _unreachableForBuildersSites = new();

  /// <summary>Sites that cannot be reached due to there is no possible paths to them.</summary>
  readonly List<ConstructionSite> _unreachableTileSites = new();

  /// <summary>Cache of tiles that have characters walking on them. It's updated each tick.</summary>
  HashSet<Vector3Int> _occupiedTiles;

  /// <summary>
  /// Cache of all possible paths to all sites. It's only updated when a nav mesh change event happens or the monitored
  /// sites are updated.
  /// </summary>
  Dictionary<ConstructionSite, List<HashSet<Vector3Int>>> _allAvailablePaths;

  PathCheckingController(DistrictCenterRegistry districtCenterRegistry, INavigationService navigationService,
                         EntityComponentRegistry entityComponentRegistry, IDistrictService districtService,
                         AutomationService automationService, BaseInstantiator baseInstantiator, ILoc loc) {
    _districtCenterRegistry = districtCenterRegistry;
    _navigationService = navigationService;
    _entityComponentRegistry = entityComponentRegistry;
    _districtService = districtService;
    _baseInstantiator = baseInstantiator;
    _loc = loc;
    automationService.EventBus.Register(this);
  }

  /// <summary>Drops conditions from the finished objects and marks the path indexes dirty.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnConstructibleEnteredFinishedStateEvent(ConstructibleEnteredFinishedStateEvent @event) {
    var site = @event.Constructible.GetComponentFast<ConstructionSite>();
    if (!_conditionsIndex.TryGetValue(site, out var conditions)) { 
      return;
    }
    foreach (var condition in conditions.ToArray()) {  // Work on copy, since it may get modified!
      if (condition.Behavior != null) {
        condition.CancelCondition();
      }
    }
    RemoveSite(@event.Constructible);
  }

  /// <summary>Marks the path indexes dirty.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent @event) {
    //FIXME: only make index dirty if monitored site is deleted.
    DebugEx.Warning("*** entity deleted");
    RemoveSite(@event.Entity);
  }

  /// <summary>Sets the condition states based on the path access check.</summary>
  void CheckBlockedAccess() {
    _occupiedTiles = null;
    foreach (var indexPair in _conditionsIndex) {
      if (_allAvailablePaths == null) {
        BuildRestrictedTilesIndex();
      }
      var site = indexPair.Key;
      var conditions = indexPair.Value;

      // Incomplete sites don't block anything.
      if (site.BuildTimeProgress < MaxCompletionProgress
          || _unreachableForBuildersSites.Contains(site)) {
        UpdateConditions(conditions, false);
        continue;
      }

      var checkCoords = site.GetComponentFast<BlockObject>().Coordinates;

      if (_occupiedTiles == null) {
        BuildOccupiedTilesIndex();
      }
      var isBlocked = _occupiedTiles.Contains(checkCoords);
      // //FIXME
      // DebugEx.Warning("*** occupant block state: site={0}, isBlocked={1}", site, isBlocked);
      if (!isBlocked) {
        foreach (var pair in _allAvailablePaths) {
          if (pair.Key == site) {
            continue;
          }
          isBlocked |= pair.Value.All(x => x.Contains(checkCoords));
          // //FIXME
          // DebugEx.Warning("*** tile block state: site={0}, isBlocked={1}, target={2}", site, isBlocked, pair.Key);
          if (isBlocked) {
            if (IsNonBlockingPathSite(site, pair.Key)) {
              //FIXME
              DebugEx.Warning("*** override non blocking path: {0}, to={1}", site, pair.Key);
              isBlocked = false;  // The path site can be completed w/o blocking the target.
            } else {
              break;
            }
          }
        }
      }
      UpdateConditions(conditions, isBlocked);
    }
  }

  /// <summary>
  /// Checks if <paramref name="pathSite"/> is a path object that doesn't block access to <paramref name="testSite"/>.
  /// </summary>
  /// <remarks>It only checks cases when the two sites are neighbours. Otherwise, the result is always false.</remarks>
  bool IsNonBlockingPathSite(ConstructionSite pathSite, ConstructionSite testSite) {
    //FIXME
    //DebugEx.Warning("*** check for path: {0} => {1}", pathSite, testSite);

    var testSiteCoords = testSite.GetComponentFast<BlockObject>().Coordinates;
    var pathSiteCoords = pathSite.GetComponentFast<BlockObject>().Coordinates;
    var coordsDelta = pathSiteCoords - testSiteCoords;
    var sqrMagnitude2d = coordsDelta.x * coordsDelta.x + coordsDelta.y * coordsDelta.y;
    if (sqrMagnitude2d > 1) {
      //DebugEx.Warning("*** too far: {0}", coordsDelta);
      return false;  // Not adjacent blocks.
    }
    var building = pathSite.GetComponentFast<Building>();
    if (building == null || !building.Path) {
      //DebugEx.Warning("*** not a path: bld={0}, isPath={1}", building, building?.Path);
      return false;  // It's not a path.
    }
    var settings = pathSite.GetComponentFast<BlockObjectNavMeshSettings>();
    if (settings == null || settings.BlockAllEdges) {
      DebugEx.Warning("*** not edges: settings={0}, allBlocked={1}", settings, settings?.BlockAllEdges);
      return false;  // No edges on the path (wtf?).
    }
    var edges = new List<Edge>(settings.ManuallySetEdges());
    if (edges.Count == 0 || edges.All(x => x.End != testSiteCoords)) {
      DebugEx.Warning("*** no edge matches: count={0}, distance={1}", edges.Count, coordsDelta);
      return false;  // No edge from the path site. 
    }
    // If any edge is connected to any district, than it's a "pass through" path to the test site.
    foreach (var district in _districtCenterRegistry.AllDistrictCenters) {
      if (edges.Any(
          x => _districtService.IsOnPreviewDistrictRoad(
              district.District, NavigationCoordinateSystem.GridToWorld(x.End)))) {
        return true;
      }
    }
    DebugEx.Warning("*** nothing matched in {0} edges", edges.Count);
    return false;
  }

  /// <summary>Gathers all coordinates that are taken by the paths to all the construction sites.</summary>
  void BuildRestrictedTilesIndex() {
    _allAvailablePaths = new Dictionary<ConstructionSite, List<HashSet<Vector3Int>>>();
    _unreachableTileSites.Clear();

    var pathCorners = new List<Vector3>();
    var allDistricts = _districtCenterRegistry.AllDistrictCenters;
    var skippedSites = 0;
    foreach (var site in _conditionsIndex.Keys) {
      var accessible = site.GetComponentFast<Accessible>();
      var allPaths = new List<HashSet<Vector3Int>>();
      if (!site.GetComponentFast<GroundedConstructionSite>().IsFullyGrounded) {
        skippedSites++;
        continue;  // Not ready to be built.
      }
      foreach (var district in allDistricts) {
        var districtPos = district.TransformFast.position;
        foreach (var access in accessible.Accesses) {
          pathCorners.Clear();
          var canDo = _navigationService.FindPathUnlimitedRange(districtPos, access, pathCorners, out _);
          if (!canDo) {
            continue;
          }
          allPaths.Add(pathCorners.Select(NavigationCoordinateSystem.WorldToGridInt).ToHashSet());
        }
      }
      if (allPaths.Count == 0) {
        _unreachableTileSites.Add(site);
        UpdateUnreachableForBuilders(site, null);
      } else {
        _allAvailablePaths[site] = allPaths;
        UpdateUnreachableForBuilders(site, accessible);
      }
    }

    // Make a full cross-join of the unreachable sites since it's not known which one will get NavMesh first.
    // FIXME: it's inefficient, so cache it.
    var crossJoinCost = 0;
    var crossJoins = 0;
    var reachableSites = _allAvailablePaths.Keys.ToList();
    foreach (var toSite in _unreachableTileSites) {
      var accessible = toSite.GetComponentFast<Accessible>();
      var allPaths = new List<HashSet<Vector3Int>>();
      foreach (var fromSite in reachableSites) {
        var fromPos = NavigationCoordinateSystem.GridToWorld(fromSite.GetComponentFast<BlockObject>().Coordinates);
        foreach (var access in accessible.Accesses) {
          ++crossJoinCost;
          pathCorners.Clear();
          var canDo = _navigationService.FindPathUnlimitedRange(fromPos, access, pathCorners, out _);
          if (!canDo) {
            continue;
          }
          ++crossJoins;
          allPaths.Add(pathCorners.Select(NavigationCoordinateSystem.WorldToGridInt).ToHashSet());
        }
      }
      //FIXME: update icons here?
      if (allPaths.Count > 0) {
        _allAvailablePaths[toSite] = allPaths;
      }
    }
    //FIXME
    DebugEx.Warning(
        "New index: indexed={0}, unreachable={1}, farFromRoad={2}, skipped={3}, crossJoinCost={4}, crossJoins={5}",
                    _allAvailablePaths.Count, _unreachableTileSites.Count, _unreachableForBuildersSites.Count,
                    skippedSites, crossJoinCost, crossJoins);
  }

  void UpdateUnreachableForBuilders(ConstructionSite site, Accessible accessible) {
    var needRemove = accessible == null || _districtService.IsOnInstantDistrictRoadSpill(accessible);
    if (needRemove) {
      if (_unreachableForBuildersSites.Remove(site)) {
        site.GetComponentFast<UnreachableStatus>().StatusToggle.Deactivate();
      }
      return;
    }
    if (!_unreachableForBuildersSites.Add(site)) {
      return;
    }
    var status = site.GetComponentFast<UnreachableStatus>();
    if (status == null) {
      status = _baseInstantiator.AddComponent<UnreachableStatus>(site.GameObjectFast);
      status.Initialize(_loc);
    }
    status.StatusToggle.Activate();
  }

  /// <summary>
  /// Gathers all coordinates that are taken by the characters paths that are in proximity to the construction sites.
  /// </summary>
  void BuildOccupiedTilesIndex() {
    _occupiedTiles = new HashSet<Vector3Int>();

    //FIXME: optimize by switching outer/inner loops: sites*districts vs occupants*districts
    //FIXME: think about checking paths for the assigned district only.
    foreach (var site in _conditionsIndex.Keys) {
      var coords = site.GetComponentFast<BlockObject>().Coordinates;
      var occupants = _entityComponentRegistry
          .GetEnabled<BlockOccupant>().Where(o => OccupantAtCoords(o, coords));
      var pathCorners = new List<Vector3>();
      var allDistricts = _districtCenterRegistry.AllDistrictCenters;
      foreach (var occupant in occupants) {
        foreach (var district in allDistricts) {
          var canDo = _navigationService.FindPathUnlimitedRange(
              district.TransformFast.position, occupant.TransformFast.position, pathCorners, out _);
          if (!canDo) {
            continue;
          }
          foreach (var pathCorner in pathCorners) {
            _occupiedTiles.Add(NavigationCoordinateSystem.WorldToGridInt(pathCorner));
          }
        }
      }
    }
    DebugEx.Fine("Created new index of occupied tiles: {0} elements", _occupiedTiles.Count);
  }

  /// <summary>Checks if the occupant is within a 3x3 block of the target coordinates.</summary>
  static bool OccupantAtCoords(BlockOccupant occupant, Vector3Int coordinates) {
    var xMin = coordinates.x - MinDistanceFromConstructionSite;
    var xMax = coordinates.x + 1 + MinDistanceFromConstructionSite;
    var yMin = coordinates.y - MinDistanceFromConstructionSite;
    var yMax = coordinates.y + 1 + MinDistanceFromConstructionSite;
    var gridCoordinates = occupant.GridCoordinates;
    var x = gridCoordinates.x;
    var y = gridCoordinates.y;
    var z = Mathf.FloorToInt(gridCoordinates.z);
    if (x >= xMin && x <= xMax && y >= yMin && y <= yMax) {
      return coordinates.z == z;
    }
    return false;
  }

  static void UpdateConditions(List<CheckAccessBlockCondition> conditions, bool isBlocked) {
    foreach (var condition in conditions) {
      condition.ConditionState = !condition.IsReversedCondition ? isBlocked : !isBlocked;
    }
  }

  /// <summary>Removes the site from all indexes.</summary>
  /// <param name="obj">Any object. If it's a known site, the indexes will be updated. Otherwise, it's a no-op.</param>
  void RemoveSite(BaseComponent obj) {
    var site = obj.GetComponentFast<ConstructionSite>();
    if (site == null) {
      return;
    }
    if (!_conditionsIndex.Remove(site)) {
      return;
    }
    DebugEx.Warning("*** Remove from index: {0}", site);
    _unreachableTileSites.Remove(site);
    _unreachableForBuildersSites.Remove(site);
    MarkIndexesDirty();
  }

  /// <summary>Forces the path indexes to rebuild on the next tick.</summary>
  void MarkIndexesDirty() {
    //FIXME
    if (_allAvailablePaths != null) {
      DebugEx.Warning("*** index dirty");
    }
    _allAvailablePaths = null;
    _occupiedTiles = null;
  }
  #endregion

  #region Helper component for custom unreachable status
  sealed class UnreachableStatus : BaseComponent {
    const string UnreachableBuilderJobLocKey = "Builders.UnreachableBuilderJob";
    const string TooFarFromPathLocKey = "IgorZ.Automation.CheckAccessBlockCondition.TooFarFromRoadAlert";
    const string StatusIconName = "UnreachableObject";

    public StatusToggle StatusToggle { get; private set; }

    void Awake() {
      enabled = false;
    }

    public void Initialize(ILoc loc) {
      StatusToggle = StatusToggle.CreatePriorityStatusWithAlertAndFloatingIcon(
          StatusIconName, loc.T(UnreachableBuilderJobLocKey), loc.T(TooFarFromPathLocKey));
      GetComponentFast<StatusSubject>().RegisterStatus(StatusToggle);
    }
  }
  #endregion
}

}
