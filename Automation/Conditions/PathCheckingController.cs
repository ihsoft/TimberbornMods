// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
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
using Timberborn.Common;
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
using Timberborn.WalkingSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace Automation.Conditions {

/// <summary>The component that handles all path checking conditions.</summary>
/// <remarks>
/// It cannot be handled in scope of just one condition due to all of them are interconnected (they can affect each
/// other). This controller has "the full picture" and orchestrates all the conditions.
/// </remarks>
sealed class PathCheckingController : ITickableSingleton, ISingletonNavMeshListener {
  const float MaxCompletionProgress = 0.8f;

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
      //FIXME
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
    var site = PathCheckingSite.GetOrCreate(condition.Behavior);
    if (!_conditionsIndex.TryGetValue(site, out var valueList)) {
      valueList = new List<CheckAccessBlockCondition>();
      _conditionsIndex[site] = valueList;
    }
    valueList.Add(condition);
    //FIXME: here we can add to the index and sync the state
    MaybeAddSite();
  }

  /// <summary>Removes the path checking condition from monitor and resets all caches.</summary>
  public void RemoveCondition(CheckAccessBlockCondition condition) {
    var site = PathCheckingSite.GetOrCreate(condition.Behavior);
    if (_conditionsIndex.TryGetValue(site, out var valueList)) {
      valueList.Remove(condition);
      if (valueList.Count == 0) {
        RemoveSite(condition.Behavior);
      }
    }
  }

  #endregion

  #region Implementation

  readonly DistrictCenterRegistry _districtCenterRegistry;
  readonly INavigationService _navigationService;
  readonly EntityComponentRegistry _entityComponentRegistry;
  readonly BaseInstantiator _baseInstantiator;
  readonly ILoc _loc;
  readonly NodeIdService _nodeIdService;
  readonly DistrictMap _districtMap;
  readonly InstantDistrictMap _instantDistrictMap;

  /// <summary>Container for the path blocking site.</summary>
  sealed class PathCheckingSite {
    #region API

    public readonly Vector3Int Coordinates;
    public readonly ConstructionSite ConstructionSite;
    public readonly GroundedConstructionSite GroundedSite;
    public readonly Accessible Accessible;
    public readonly List<Edge> Edges;

    /// <summary>Find the existing construction site or creates a new one.</summary>
    /// <returns>The site or <c>null</c> if the <paramref name="component"/> is not eligible to be a site.</returns>
    public static PathCheckingSite GetOrCreate(BaseComponent component) {
      var site = new PathCheckingSite(component);
      if (!site._blockObject || !site.ConstructionSite || !site.GroundedSite || !site.Accessible) {
        return null;
      }
      if (!_controller._sitesByCoordinates.TryGetValue(site.Coordinates, out var cachedSite)) {
        _controller._sitesByCoordinates.Add(site.Coordinates, site);
        cachedSite = site;
      }
      return cachedSite;
    }

    /// <summary>Tries to lookup the site by its coordinates.</summary>
    public static bool TryGet(BaseComponent component, out PathCheckingSite cachedSite) {
      var site = new PathCheckingSite(component);
      if (!site._blockObject || !site.ConstructionSite || !site.GroundedSite || !site.Accessible) {
        cachedSite = null;
        return false;
      }
      return _controller._sitesByCoordinates.TryGetValue(site.Coordinates, out cachedSite);
    }

    /// <summary>Removes teh site from the caches.</summary>
    public void Remove() {
      _controller._sitesByCoordinates.Remove(Coordinates);
      if (_unreachableStatus) {
        _unreachableStatus.Cleanup();
      }
    }

    #endregion

    #region Object overrides

    /// <inheritdoc/>
    public override int GetHashCode() {
      return Coordinates.GetHashCode();
    }

    /// <inheritdoc/>
    public override bool Equals(object obj) {
      return obj is PathCheckingSite checkingSite && checkingSite.Coordinates.Equals(Coordinates);
    }

    #endregion

    #region Implementation

    internal static PathCheckingController _controller;
    readonly BlockObject _blockObject;
    UnreachableStatus _unreachableStatus;

    PathCheckingSite(BaseComponent component) {
      _blockObject = component.GetComponentFast<BlockObject>();
      if (!_blockObject) {
        return;
      }
      Coordinates = _blockObject.Coordinates;
      ConstructionSite = component.GetComponentFast<ConstructionSite>();
      GroundedSite = component.GetComponentFast<GroundedConstructionSite>();
      Accessible = component.GetComponentFast<Accessible>();
      _unreachableStatus = component.GetComponentFast<UnreachableStatus>();

      var building = component.GetComponentFast<Building>();
      if (building && building.Path) {
        var settings = component.GetComponentFast<BlockObjectNavMeshSettings>();
        if (settings && !settings.BlockAllEdges) {
          var manuallySetEdges = settings.ManuallySetEdges().ToList();
          if (manuallySetEdges.Count > 0) {
            Edges = manuallySetEdges;
          }
        }
      }
    }

    /// <summary>Executes the action if there is a <see cref="UnreachableStatus"/> component on the building.</summary>
    public void ActOnExistingUnreachable(Action<UnreachableStatus> fn) {
      if (_unreachableStatus) {
        fn.Invoke(_unreachableStatus);
      }
    }

    /// <summary>Gets the existing or creates a new <see cref="UnreachableStatus"/> and executes the action.</summary>
    public void ActOnRequiredUnreachable(Action<UnreachableStatus> fn) {
      if (!_unreachableStatus) {
        _unreachableStatus =
            _controller._baseInstantiator.AddComponent<UnreachableStatus>(ConstructionSite.GameObjectFast);
        _unreachableStatus.Initialize(_controller._loc);
      }
      fn.Invoke(_unreachableStatus);
    }

    #endregion
  }

  /// <summary>All path checking conditions on the site.</summary>
  readonly Dictionary<PathCheckingSite, List<CheckAccessBlockCondition>> _conditionsIndex = new();

  /// <summary>Cache of tiles that are paths to the characters on the map.</summary>
  HashSet<Vector3Int> _walkersTakenTiles;

  /// <summary>Cache of the walking characters positions.</summary>
  /// <remarks>
  /// If a character is at teh site being constructed, we don't block since the game checks it naturally.
  /// </remarks>
  HashSet<Vector3Int> _walkersCoords;

  /// <summary>Cache of all possible paths to all the construction sites marked for the condition.</summary>
  /// <remarks>
  /// If this cache needs to be updated, call <see cref="MarkIndexesDirty"/>. Be wise and don't update the index
  /// frequently as it's a very expensive operation. Navmesh updates and new sites would certainly need the index
  /// update. Anything else is negotiable.
  /// </remarks>
  Dictionary<PathCheckingSite, HashSet<Vector3Int>> _allKnownPaths;

  /// <summary>Sites that are either too far or don't have accessible at the same level.</summary>
  HashSet<PathCheckingSite> _unreachableSites;

  /// <summary>All path sites for all conditions.</summary>
  readonly Dictionary<Vector3Int, PathCheckingSite> _sitesByCoordinates = new();

  PathCheckingController(DistrictCenterRegistry districtCenterRegistry, INavigationService navigationService,
                         EntityComponentRegistry entityComponentRegistry, AutomationService automationService,
                         BaseInstantiator baseInstantiator, ILoc loc, NodeIdService nodeIdService,
                         DistrictMap districtMap, InstantDistrictMap instantDistrictMap) {
    _districtCenterRegistry = districtCenterRegistry;
    _navigationService = navigationService;
    _entityComponentRegistry = entityComponentRegistry;
    _baseInstantiator = baseInstantiator;
    _loc = loc;
    _nodeIdService = nodeIdService;
    _districtMap = districtMap;
    _instantDistrictMap = instantDistrictMap;
    automationService.EventBus.Register(this);
    PathCheckingSite._controller = this;
  }

  /// <summary>Sets the condition states based on the path access check.</summary>
  void CheckBlockedAccess() {
    _walkersTakenTiles = null;
    foreach (var indexPair in _conditionsIndex) {
      if (_allKnownPaths == null) {
        BuildRestrictedTilesIndex();
      }
      var site = indexPair.Key;
      var conditions = indexPair.Value;

      // Incomplete sites don't block anything.
      if (site.ConstructionSite.BuildTimeProgress < MaxCompletionProgress) {
        UpdateConditions(conditions, false);
        continue;
      }

      var checkCoords = site.Coordinates;
      var isBlocked = false;
      foreach (var pair in _allKnownPaths) {
        if (!ReferenceEquals(pair.Key, site)
            && pair.Value.Contains(checkCoords)
            && !IsNonBlockingPathSite(site, pair.Key, pair.Value)) {
          isBlocked = true;
          break;
        }
      }
      if (!isBlocked) {
        if (_walkersTakenTiles == null) {
          BuildWalkersIndex();
        }
        isBlocked = _walkersTakenTiles.Contains(checkCoords) && !_walkersCoords.Contains(checkCoords);
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
  static bool IsNonBlockingPathSite(
      PathCheckingSite pathSite, PathCheckingSite testSite, HashSet<Vector3Int> testPath) {
    if (pathSite.Edges == null) {
      return false;  // Not a path building.
    }
    var coordsDelta = pathSite.Coordinates - testSite.Coordinates;
    if (coordsDelta.z > 0 || coordsDelta.x > 1 || coordsDelta.y > 1 || coordsDelta.x == coordsDelta.y) {
      return false;
    }
    return pathSite.Edges.Any(edge => testPath.Contains(edge.End) && testPath.Contains(edge.End));
  }

  /// <summary>Gets the best (the shortest) path to the site from the closets road node.</summary>
  /// <remarks>
  /// The site must be within the range from the road, which is currently 9 tiles. We intentionally ignore the accesses
  /// below the site. Even though the game can construct sites this way (one tile above the road), the path checking
  /// algo cannot deal with all the corners cases. Thus, just block such situations. It may limit players in their
  /// construction methods, but that's the price to pay.
  /// </remarks>
  HashSet<Vector3Int> GetBestPath(PathCheckingSite site) {
    var bestDistance = float.MaxValue;
    var bestRoadNode = -1;
    var bestAccess = Vector3.zero;
    
    var worldCoords = CoordinateSystem.GridToWorld(site.Coordinates);
    var accesses = site.Accessible.Accesses
        .Where(access => access.y >= worldCoords.y && _nodeIdService.Contains(access));
    foreach (var access in accesses) {
      var accessNode = _nodeIdService.WorldToId(access);
      foreach (var district in _districtCenterRegistry.AllDistrictCenters) {
        var flow = _districtMap.GetDistrictRoadSpillFlowField(district.District);
        if (!flow.HasNode(accessNode)) {
          continue;
        }
        var newDistance = flow.GetDistanceToRoad(accessNode);
        if (newDistance >= bestDistance) {
          continue;
        }
        bestDistance = newDistance;
        bestRoadNode = flow.GetRoadParentNodeId(accessNode);
        bestAccess = access;
      }
    }
    if (bestRoadNode == -1) {
      return null;  // The site is unreachable.
    }

    var bestRoadPosition = _nodeIdService.IdToWorld(bestRoadNode);
    var pathCorners = new List<Vector3>();
    _navigationService.FindPathUnlimitedRange(bestRoadPosition, bestAccess, pathCorners, out _);
    return pathCorners.Select(NavigationCoordinateSystem.WorldToGridInt).ToHashSet();
  }

  /// <summary>Gathers all coordinates that are taken by the paths to the construction sites.</summary>
  /// <remarks>
  /// Don't consider sites that are not yet close to completion or are not blocking sites (like a path building).
  /// </remarks>
  void BuildRestrictedTilesIndex() {
    var stopwatch = Stopwatch.StartNew();
    _allKnownPaths = new Dictionary<PathCheckingSite, HashSet<Vector3Int>>();
    _unreachableSites = new HashSet<PathCheckingSite>();

    var skippedSites = 0;
    foreach (var site in _conditionsIndex.Keys) {
      if (!site.GroundedSite.IsFullyGrounded) {
        skippedSites++;
        continue;  // Not ready to be built.
      }
      var path = GetBestPath(site);
      if (path == null) {
        _unreachableSites.Add(site);
        site.ActOnRequiredUnreachable(x => x.SetUnreachable());
      } else {
        site.ActOnExistingUnreachable(x => x.Cleanup());
        _allKnownPaths[site] = path;
      }
    }
    stopwatch.Stop();
    //FIXME
    DebugEx.Warning("New index: indexed={0}, unreachable={1}, skipped={2}, time={3}ms",
                    _allKnownPaths.Count, _unreachableSites.Count, skippedSites, stopwatch.ElapsedMilliseconds);
  }

  /// <summary>Gathers all coordinates that are taken by the characters paths.</summary>
  /// <remarks>We don't want to let the builders get stranded.</remarks>
  void BuildWalkersIndex() {
    _walkersTakenTiles = new HashSet<Vector3Int>();
    _walkersCoords = new HashSet<Vector3Int>();
    var walkers = _entityComponentRegistry
        .GetEnabled<BlockOccupant>()
        .Select(x => x.GetComponentFast<Walker>())
        .Where(x => x);
    foreach (var walker in walkers) {
      _walkersCoords.Add(NavigationCoordinateSystem.WorldToGridInt(walker.TransformFast.position));
      var pathFollower = walker._pathFollower;
      if (pathFollower._pathCorners == null) {
        continue;  // No path, no problem.
      }
      var activePathCorners = pathFollower._pathCorners
          .Skip(pathFollower._nextCornerIndex - 1)
          .Select(CoordinateSystem.WorldToGridInt);
      _walkersTakenTiles.AddRange(activePathCorners);
    }
  }

  /// <summary>Triggers the provided path checking conditions.</summary>
  static void UpdateConditions(List<CheckAccessBlockCondition> conditions, bool isBlocked) {
    foreach (var condition in conditions) {
      condition.ConditionState = !condition.IsReversedCondition ? isBlocked : !isBlocked;
    }
  }

  /// <summary>Adds the site from to the indexes.</summary>
  void MaybeAddSite() {
    //FIXME: make it iterative
    MarkIndexesDirty();
  }

  /// <summary>Removes the site from all indexes.</summary>
  /// <param name="obj">If it's a known site, the indexes will be updated. Otherwise, it's a no-op.</param>
  void RemoveSite(BaseComponent obj) {
    if (!PathCheckingSite.TryGet(obj, out var site)) {
      return;
    }
    _conditionsIndex.Remove(site);
    _allKnownPaths?.Remove(site);
    site.Remove();
  }

  /// <summary>Forces the path indexes to rebuild on the next tick.</summary>
  void MarkIndexesDirty() {
    _allKnownPaths = null;
  }

  #endregion

  #region BaseComponent for custom unreachable status

  sealed class UnreachableStatus : BaseComponent, ISelectionListener {
    const string NotYetReachableLocKey = "IgorZ.Automation.CheckAccessBlockCondition.NotYetReachable";
    const string UnreachableIconName = "UnreachableObject";

    StatusToggle _unreachableStatusToggle;
    BlockableBuilding _blockableBuilding;
    BuilderJobReachabilityStatus _builderJobReachabilityStatus;
    bool _isSelected;

    void Awake() {
      _blockableBuilding = GetComponentFast<BlockableBuilding>();
      _builderJobReachabilityStatus = GetComponentFast<BuilderJobReachabilityStatus>();
    }

    /// <summary>Initializes the component since the normal Bindito logic doesn't work here.</summary>
    public void Initialize(ILoc loc) {
      _unreachableStatusToggle = StatusToggle.CreateNormalStatusWithFloatingIcon(
          UnreachableIconName, loc.T(NotYetReachableLocKey));
      GetComponentFast<StatusSubject>().RegisterStatus(_unreachableStatusToggle);
    }

    /// <summary>
    /// The unreachable sites cannot be built.
    /// </summary>
    /// <remarks>
    /// Our meaning of "unreachable" can be different from the game's point of view. For the algorithm, it's important
    /// to not have such sites started until we allow it to. Thus, we block such sites.
    /// </remarks>
    public void SetUnreachable() {
      _unreachableStatusToggle.Activate();
      _blockableBuilding.Block(this);
    }

    /// <summary>A cleanup method that resets all effect on the site.</summary>
    public void Cleanup() {
      _unreachableStatusToggle.Deactivate();
      _blockableBuilding.Unblock(this);
      if (_isSelected && _builderJobReachabilityStatus) {
        _builderJobReachabilityStatus.OnSelect();
      }
    }

    #region ISelectionListener implemenation

    /// <inheritdoc/>
    public void OnSelect() {
      _isSelected = true;
      if (!_builderJobReachabilityStatus) {
        return;
      }
      if (_unreachableStatusToggle.IsActive) {
        _builderJobReachabilityStatus.OnUnselect();  // We show our own status.
      }
    }

    /// <inheritdoc/>
    public void OnUnselect() {
      _isSelected = false;
    }

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
    if (!PathCheckingSite.TryGet(@event.Constructible, out var site)) {
      return;
    }
    var conditions = _conditionsIndex[site];
    foreach (var condition in conditions.ToArray()) {  // Work on copy, since it may get modified.
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
