// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Automation.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlockSystemNavigation;
using Timberborn.BuilderHubSystem;
using Timberborn.Buildings;
using Timberborn.BuildingsBlocking;
using Timberborn.ConstructibleSystem;
using Timberborn.ConstructionSites;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Localization;
using Timberborn.Navigation;
using Timberborn.SelectionSystem;
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

  /// <inheritdoc/>
  public void Tick() {
    _tickStopwatch.Start();
    CheckBlockedAccess();
    _tickStopwatch.Stop();
    if (--_ticksCount <= 0) {
      _ticksCount = StatsLoggingTicks;
      var elapsed = _tickStopwatch.ElapsedMilliseconds;
      _tickStopwatch.Reset();
      DebugEx.Warning("*** path controller cost: total={0}ms, avg={1:0.00}ms", elapsed, (float)elapsed / StatsLoggingTicks);
    }
  }
  readonly Stopwatch _tickStopwatch = new();
  const int StatsLoggingTicks = 10;
  int _ticksCount = StatsLoggingTicks;

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
    if (customStatus) {
      customStatus.Cleanup();
    }
    if (_conditionsIndex.TryGetValue(site, out var valueList)) {
      valueList.Remove(condition);
      if (valueList.Count == 0) {
        RemoveSite(site);
      }
    }
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

  /// <summary>Cache of tiles that are paths to the characters nearby.</summary>
  HashSet<Vector3Int> _occupantsTiles;

  /// <summary>Exact locations of all occupants that cross the sites.</summary>
  /// <remarks>Don't block such sites since the game will handle it better.</remarks>
  HashSet<Vector3Int> _occupants;

  /// <summary>Cache of all possible paths to all the construction sites marked for the condition.</summary>
  /// <remarks>
  /// If this cache needs to be updated, call <see cref="MarkIndexesDirty"/>. Be wise and don't update the index
  /// frequently as it's a very expensive operation. Navmesh updates and new sites would certainly need the index
  /// update. Anything else is negotiable.
  /// </remarks>
  Dictionary<ConstructionSite, List<HashSet<Vector3Int>>> _allKnownPaths;

  /// <summary>Sites that are either too far or don't have accessible at the same level.</summary>
  HashSet<ConstructionSite> _unreachableSites;

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

  /// <summary>Sets the condition states based on the path access check.</summary>
  void CheckBlockedAccess() {
    _occupantsTiles = null;
    foreach (var indexPair in _conditionsIndex) {
      if (_allKnownPaths == null) {
        BuildRestrictedTilesIndex();
      }
      var site = indexPair.Key;
      var conditions = indexPair.Value;

      // Incomplete sites don't block anything.
      if (site.BuildTimeProgress < MaxCompletionProgress) {
        UpdateConditions(conditions, false);
        continue;
      }

      var checkCoords = site.GetComponentFast<BlockObject>().Coordinates;
      var isBlocked = false;
      foreach (var pair in _allKnownPaths) {
        if (pair.Key != site
            && pair.Value.All(x => x.Contains(checkCoords)) && !IsNonBlockingPathSite(site, pair.Key, pair.Value)) {
          isBlocked = true;
          break;
        }
      }
      if (!isBlocked) {
        if (_occupantsTiles == null) {
          BuildOccupiedTilesIndex();
        }
        isBlocked = _occupantsTiles.Contains(checkCoords) && !_occupants.Contains(checkCoords);
      }
      UpdateConditions(conditions, isBlocked);
    }
  }

  /// <summary>
  /// Checks if <paramref name="pathSite"/> is a path object that doesn't block access to <paramref name="testSite"/>.
  /// </summary>
  /// <remarks>
  /// It only checks cases when the two sites are neighbours and at the same level. Otherwise, the result is always
  /// negative.
  /// </remarks>
  bool IsNonBlockingPathSite(
      ConstructionSite pathSite, ConstructionSite testSite, List<HashSet<Vector3Int>> testPaths) {
    //FIXME: index all path builidngs in advance.
    var pathSiteCoords = pathSite.GetComponentFast<BlockObject>().Coordinates;
    var testSiteCoords = testSite.GetComponentFast<BlockObject>().Coordinates;
    var coordsDelta = pathSiteCoords - testSiteCoords;
    if (coordsDelta.z > 0) {
      DebugEx.Warning("*** skip vertical neighbours: pathSite={0}, testSite={1}", pathSite, testSite);
      return false;
    }
    if (coordsDelta.x > 1 || coordsDelta.y > 1 || coordsDelta.x == coordsDelta.y) {
      return false;
    }
    var building = pathSite.GetComponentFast<Building>();
    if (!building || !building.Path) {
      return false;  // It's not a path.
    }
    var settings = pathSite.GetComponentFast<BlockObjectNavMeshSettings>();
    if (!settings || settings.BlockAllEdges) {
      return false;  // No edges on the path (wtf?).
    }
    var edges = new List<Edge>(settings.ManuallySetEdges());
    if (edges.Count == 0) {
      return false;  // No edge from the path site. 
    }
    return edges.Any(edge => testPaths.Any(x => x.Contains(edge.End) && x.Contains(edge.End)));
  }

  /// <summary>Gathers all coordinates that are taken by the paths to all the construction sites.</summary>
  void BuildRestrictedTilesIndex() {
    var stopwatch = Stopwatch.StartNew();
    _allKnownPaths = new Dictionary<ConstructionSite, List<HashSet<Vector3Int>>>();
    _unreachableSites = new HashSet<ConstructionSite>();

    var pathCorners = new List<Vector3>();
    var allDistricts = _districtCenterRegistry.AllDistrictCenters;
    var skippedSites = 0;
    foreach (var site in _conditionsIndex.Keys) {
      var accessible = site.GetComponentFast<Accessible>();
      var blockObject = site.GetComponentFast<BlockObject>();
      var blockWorldCoords = CoordinateSystem.GridToWorld(blockObject.Coordinates);
      var allPaths = new List<HashSet<Vector3Int>>();
      if (!site.GetComponentFast<GroundedConstructionSite>().IsFullyGrounded) {
        skippedSites++;
        continue;  // Not ready to be built.
      }
      foreach (var district in allDistricts) {
        var districtPos = district.TransformFast.position;
        foreach (var access in accessible.Accesses) {
          if (access.y < blockWorldCoords.y) {
            continue;
          }
          pathCorners.Clear();
          var canDo = _navigationService.FindPathUnlimitedRange(districtPos, access, pathCorners, out _);
          if (!canDo) {
            continue;
          }
          allPaths.Add(pathCorners.Select(NavigationCoordinateSystem.WorldToGridInt).ToHashSet());
        }
      }
      // The site can be reachable, but too far from the road.
      //FIXME: we can get road node via RoadSpillFlowField and check path against it? 
      if (allPaths.Count == 0) {
        _unreachableSites.Add(site);
        GetUnreachableStatus(site).SetUnreachable();
      } else {
        if (!_districtService.IsOnInstantDistrictRoadSpill(accessible)) {
          GetUnreachableStatus(site).SetTooFarFromRoad();
        } else {
          var status = site.GetComponentFast<UnreachableStatus>();
          if (status) {
            status.Cleanup();
          }
        }
        _allKnownPaths[site] = allPaths;
      }
    }
    stopwatch.Stop();
    //FIXME
    DebugEx.Warning("New index: indexed={0}, unreachable={1}, skipped={2}, time={3}ms",
                    _allKnownPaths.Count, _unreachableSites.Count, skippedSites, stopwatch.ElapsedMilliseconds);
  }

  UnreachableStatus GetUnreachableStatus(ConstructionSite site) {
    var status = site.GetComponentFast<UnreachableStatus>();
    if (!status) {
      status = _baseInstantiator.AddComponent<UnreachableStatus>(site.GameObjectFast);
      status.Initialize(_loc);
    }
    return status;
  }

  /// <summary>
  /// Gathers all coordinates that are taken by the characters paths that are in proximity to the construction sites.
  /// </summary>
  void BuildOccupiedTilesIndex() {
    var stopwatch = Stopwatch.StartNew();
    _occupantsTiles = new HashSet<Vector3Int>();
    _occupants = new HashSet<Vector3Int>();

    //FIXME: optimize by switching outer/inner loops: sites*districts vs occupants*districts
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
          _occupants.Add(NavigationCoordinateSystem.WorldToGridInt(occupant.TransformFast.position));
          foreach (var pathCorner in pathCorners) {
            _occupantsTiles.Add(NavigationCoordinateSystem.WorldToGridInt(pathCorner));
          }
        }
      }
    }
    stopwatch.Stop();
    //FIXME
    DebugEx.Warning("Created new index of occupied tiles: {0} elements, cost={1}ms",
                    _occupantsTiles.Count, stopwatch.ElapsedMilliseconds);
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

  /// <summary>Triggers the provided path checking conditions.</summary>
  static void UpdateConditions(List<CheckAccessBlockCondition> conditions, bool isBlocked) {
    foreach (var condition in conditions) {
      condition.ConditionState = !condition.IsReversedCondition ? isBlocked : !isBlocked;
    }
  }

  /// <summary>Removes the site from all indexes.</summary>
  /// <param name="obj">If it's a known site, the indexes will be updated. Otherwise, it's a no-op.</param>
  void RemoveSite(BaseComponent obj) {
    var site = obj.GetComponentFast<ConstructionSite>();
    if (!site) {
      return;
    }
    _conditionsIndex.Remove(site);
    _allKnownPaths?.Remove(site);
    var status = site.GetComponentFast<UnreachableStatus>();
    if (status) {
      status.Cleanup();
    }
  }

  /// <summary>Forces the path indexes to rebuild on the next tick.</summary>
  void MarkIndexesDirty() {
    _allKnownPaths = null;
  }

  #endregion

  #region BaseComponent for custom unreachable status

  sealed class UnreachableStatus : BaseComponent, ISelectionListener {
    const string UnreachableBuilderJobLocKey = "Builders.UnreachableBuilderJob";
    const string UnreachableFromSameLevelLocKey = "IgorZ.Automation.CheckAccessBlockCondition.UnreachableFromSameLevel";
    const string TooFarFromPathLocKey = "IgorZ.Automation.CheckAccessBlockCondition.TooFarFromRoadAlert";
    const string UnreachableIconName = "UnreachableObject";

    StatusToggle _tooFarFromRoadStatusToggle;
    StatusToggle _unreachableStatusToggle;
    BlockableBuilding _blockableBuilding;
    BuilderJobReachabilityStatus _builderJobReachabilityStatus;

    void Awake() {
      _blockableBuilding = GetComponentFast<BlockableBuilding>();
      _builderJobReachabilityStatus = GetComponentFast<BuilderJobReachabilityStatus>();
    }

    /// <summary>Initializes the component since the normal Bindito logic doesn't work here.</summary>
    public void Initialize(ILoc loc) {
      _tooFarFromRoadStatusToggle = StatusToggle.CreateNormalStatusWithAlertAndFloatingIcon(
          UnreachableIconName, loc.T(UnreachableBuilderJobLocKey), loc.T(TooFarFromPathLocKey));
      GetComponentFast<StatusSubject>().RegisterStatus(_tooFarFromRoadStatusToggle);
      _unreachableStatusToggle = StatusToggle.CreateNormalStatus(
          UnreachableIconName, loc.T(UnreachableFromSameLevelLocKey));
      GetComponentFast<StatusSubject>().RegisterStatus(_unreachableStatusToggle);
    }

    /// <summary>
    /// The too far sites cannot be built, but they are reachable and we want them to affect the conditions.
    /// </summary>
    public void SetTooFarFromRoad() {
      _tooFarFromRoadStatusToggle.Activate();
      _unreachableStatusToggle.Deactivate();
      _blockableBuilding.Unblock(this);
    }

    /// <summary>
    /// The unreachable sites cannot be built.
    /// </summary>
    /// <remarks>
    /// Our meaning of "unreachable" can be different from the game's point of view. For teh algorithm, it's important
    /// to not have such sites built. Thus, we block such sites to ensure the game won't start building them.
    /// </remarks>
    public void SetUnreachable() {
      _tooFarFromRoadStatusToggle.Deactivate();
      _unreachableStatusToggle.Activate();
      _blockableBuilding.Block(this);
    }

    /// <summary>A cleanup method that resets all effect on the site.</summary>
    public void Cleanup() {
      _tooFarFromRoadStatusToggle.Deactivate();
      _unreachableStatusToggle.Deactivate();
      _blockableBuilding.Unblock(this);
    }

    #region ISelectionListener implemenation

    /// <inheritdoc/>
    public void OnSelect() {
      if (!_builderJobReachabilityStatus) {
        return;
      }
      if (_unreachableStatusToggle.IsActive || _tooFarFromRoadStatusToggle.IsActive) {
        _builderJobReachabilityStatus.OnUnselect();  // We show our own status.
      }
    }

    /// <inheritdoc/>
    public void OnUnselect() {}

    #endregion
  }

  #endregion

  #region ISingletonNavMeshListener implemenation

  /// <inheritdoc/>
  public void OnNavMeshUpdated(NavMeshUpdate navMeshUpdate) {
    MarkIndexesDirty();
  }

  #endregion

  #region Events

  /// <summary>Drops conditions from the finished objects and marks the path indexes dirty.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnConstructibleEnteredFinishedStateEvent(ConstructibleEnteredFinishedStateEvent @event) {
    var site = @event.Constructible.GetComponentFast<ConstructionSite>();
    if (!_conditionsIndex.TryGetValue(site, out var conditions)) { 
      return;
    }
    foreach (var condition in conditions.ToArray()) {  // Work on copy, since it may get modified!
      if (condition.Behavior) {
        condition.CancelCondition();
      }
    }
    RemoveSite(@event.Constructible);
  }

  /// <summary>Marks the path indexes dirty.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent @event) {
    RemoveSite(@event.Entity);
  }

  #endregion
}

}
