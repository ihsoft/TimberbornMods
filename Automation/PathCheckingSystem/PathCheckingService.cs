// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Automation.AutomationSystem;
using Automation.Conditions;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Navigation;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace Automation.PathCheckingSystem {

/// <summary>The component that handles all path checking conditions.</summary>
/// <remarks>
/// It cannot be handled in scope of just one condition due to all of them are interconnected (they can affect each
/// other). This controller has "the full picture" and orchestrates all the conditions.
/// </remarks>
sealed class PathCheckingService : ITickableSingleton, ISingletonNavMeshListener {

  #region ITickableSingleton implementation

  /// <inheritdoc/>
  public void Tick() {
    if (Features.PathCheckingSystemProfiling) {
      Profile();
    }
  }

  #endregion

  #region API

  /// <summary>Shortcut for the Harmony patches.</summary>
  /// <remarks>All normal classes should inject the service.</remarks>
  internal static PathCheckingService Instance;

  /// <summary>Add the path checking condition to monitor.</summary>
  public void AddCondition(CheckAccessBlockCondition condition) {
    var site = GetOrCreate(condition.Behavior.BlockObject);
    _conditionsIndex.GetOrAdd(site).Add(condition);
  }

  /// <summary>Removes the path checking condition from monitor and resets all caches.</summary>
  public void RemoveCondition(CheckAccessBlockCondition condition) {
    if (!_sitesByBlockObject.TryGetValue(condition.Behavior.BlockObject, out var site)) {
      DebugEx.Warning("Unknown condition {0} on behavior {1}", condition, condition.Behavior);
      return;
    }
    if (_conditionsIndex.TryGetValue(site, out var valueList)) {
      valueList.Remove(condition);
      if (valueList.Count == 0) {
        DeleteSite(site);
      }
    }
  }

  #endregion

  #region Implementation

  readonly EntityComponentRegistry _entityComponentRegistry;
  readonly NodeIdService _nodeIdService;
  readonly BaseInstantiator _baseInstantiator;
  readonly DistrictMap _districtMap;

  /// <summary>All path checking conditions on the sites.</summary>
  readonly Dictionary<PathCheckingSite, List<CheckAccessBlockCondition>> _conditionsIndex = new();

  /// <summary>All path sites index by blockobject.</summary>
  readonly Dictionary<BlockObject, PathCheckingSite> _sitesByBlockObject = new();

  /// <summary>Cache of tiles that are paths to the characters on the map.</summary>
  HashSet<int> _walkersTakenNodes;

  /// <summary>Tells whether the NvaMesh is now initialized and ready to use.</summary>
  bool _navMeshIsReady;

  PathCheckingService(EntityComponentRegistry entityComponentRegistry, AutomationService automationService,
                      NodeIdService nodeIdService, BaseInstantiator baseInstantiator, DistrictMap districtMap) {
    Instance = this;
    _entityComponentRegistry = entityComponentRegistry;
    _nodeIdService = nodeIdService;
    _baseInstantiator = baseInstantiator;
    _districtMap = districtMap;
    automationService.EventBus.Register(this);
  }

  /// <summary>
  /// Runs the checks to update <see cref="PathCheckingSite.CanFinish"/> and triggers the relevant conditions.
  /// conditions.
  /// </summary>
  public void CheckBlockingStateAndTriggerActions(PathCheckingSite site) {
    site.CanFinish = !IsBlockingSite(site);
    var conditions = _conditionsIndex[site];
    foreach (var condition in conditions) {
      condition.ConditionState = condition.IsReversedCondition ? site.CanFinish : !site.CanFinish;
    }
  }

  /// <summary>Determines if the site construction can complete without obstructing any other site.</summary>
  bool IsBlockingSite(PathCheckingSite site) {
    if (site.BestAccessNode == -1 || site.RestrictedNodes.Count == 0) {
      return false;  // This site cannot block anything.
    }
    PathCheckProfiler.StartNewHit();
    var isBlocked = false;
    foreach (var testSite in _sitesByBlockObject.Values) {
      if (testSite.BestAccessNode != -1 && !ReferenceEquals(testSite, site) && !IsNonBlockingPathSite(site, testSite)) {
        isBlocked = true;
        break;
      }
    }
    if (!isBlocked) {
      PathCheckProfiler.Stop();
      MaybeBuildWalkersIndex();
      PathCheckProfiler.Start();
      isBlocked = site.RestrictedNodes.FastAny(x => _walkersTakenNodes.Contains(x));
    }
    PathCheckProfiler.Stop();
    return isBlocked;
  }

  /// <summary>
  /// Checks if <paramref name="pathSite"/> is a path object that doesn't block access to <paramref name="testSite"/>.
  /// </summary>
  static bool IsNonBlockingPathSite(PathCheckingSite pathSite, PathCheckingSite testSite) {
    var testPathCorners = testSite.BestBuildersPathCornerNodes;
    if (testPathCorners.Count == 1) {
      return true;  // This site stays right at the road. Nothing can block it.
    }
    var testPathNodes = testSite.BestBuildersPathNodeIndex;
    var edges = pathSite.NodeEdges;
    for (var i = pathSite.RestrictedNodes.Count - 1; i >= 0; i--) {
      var restrictedCoordinate = pathSite.RestrictedNodes[i];
      if (!testPathNodes.Contains(restrictedCoordinate)) {
        continue;
      }
      var pathPos = testPathCorners.IndexOf(pathSite.SiteNodeId);

      // If the blocking site is at the access, then just check if it has and an edge to the test site.
      if (pathPos == 0) {
        if (edges.FastAny(e => e.Start == restrictedCoordinate && e.End == testSite.BestAccessNode)) {
          continue;
        }
        return false;
      }

      // If the blocking site is in the middle of the path, then verify if the nodes before and after are connected by
      // any edge. This would mean that the site provides connectivity and doesn't block the path.
      var nodeAfter = testPathCorners[pathPos - 1];
      var nodeBefore = testPathCorners[pathPos + 1];
      var isConnectedToTestSite = false;
      var isConnectedToTheTestPath = false;
      for (var j = edges.Count - 1; j >= 0; j--) {
        var edge = edges[i];
        if (edge.Start != restrictedCoordinate) {
          continue;
        }
        if (!isConnectedToTestSite && edge.End == nodeAfter) {
          isConnectedToTestSite = true;
        }
        if (!isConnectedToTheTestPath && edge.End == nodeBefore) {
          isConnectedToTheTestPath = true;
        }
        if (isConnectedToTestSite && isConnectedToTheTestPath) {
          break;
        }
      }
      if (!isConnectedToTestSite || !isConnectedToTheTestPath) {
        return false;
      }
    }
    return true;
  }

  /// <summary>Gathers all coordinates that are taken by the characters paths.</summary>
  /// <remarks>We don't want to let the builders get stranded.</remarks>
  void MaybeBuildWalkersIndex() {
    if (_lastCacheBuiltFrame == Time.frameCount) {
      return; // The cache is up to date.
    }
    WalkersCheckProfiler.StartNewHit();
    _lastCacheBuiltFrame = Time.frameCount;
    _walkersTakenNodes = new HashSet<int>();
    var citizens = _entityComponentRegistry.GetEnabled<BlockOccupant>()
        .Select(x => x.GetComponentFast<Citizen>())
        .Where(x => x.HasAssignedDistrict);
    foreach (var citizen in citizens) {
      var pathNodes = GetPathToRoadFast(citizen.TransformFast.position, citizen.AssignedDistrict.District);
      if (pathNodes != null) {
        _walkersTakenNodes.AddRange(pathNodes);
      }
    }
    WalkersCheckProfiler.Stop();
  }
  int _lastCacheBuiltFrame;

  /// <summary>Fast method of getting path to the closest road.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  List<int> GetPathToRoadFast(Vector3 worldStart, District district) {
    var flow = _districtMap.GetDistrictRoadSpillFlowField(district);
    var nodeId = _nodeIdService.WorldToId(worldStart);
    if (!flow.HasNode(nodeId)) {
      return null;
    }
    var res = new List<int>(20) {
        nodeId
    };
    var roadParentId = flow.GetRoadParentNodeId(nodeId);
    if (nodeId == roadParentId) {
      return res;  // Already on the road.
    }
    do {
      nodeId = flow.GetParentId(nodeId);
      res.Add(nodeId);
    } while (nodeId != roadParentId);
    return res;
  }

  /// <summary>Deletes site if the matching component is in index.</summary>
  void TryDeleteSite(BaseComponent component) {
    var blockObject = component.GetComponentFast<BlockObject>();
    if (blockObject && _sitesByBlockObject.TryGetValue(blockObject, out var site)) {
      DeleteSite(site);
    }
  }

  /// <summary>Finds the existing construction site or creates a new one.</summary>
  PathCheckingSite GetOrCreate(BlockObject blockObject) {
    if (!_sitesByBlockObject.TryGetValue(blockObject, out var cachedSite)) {
      cachedSite = blockObject.GetComponentFast<PathCheckingSite>();
      if (cachedSite) {
        cachedSite.EnableComponent();
      } else {
        cachedSite = _baseInstantiator.AddComponent<PathCheckingSite>(blockObject.GameObjectFast);
        if (_navMeshIsReady) { // No mesh means it's a loading stage. The game will init the site. 
          cachedSite.InitializeEntity();
        }
      }
      _sitesByBlockObject.Add(blockObject, cachedSite);
    }
    return cachedSite;
  }

  /// <summary>Removes the site and all the associated conditions.</summary>
  void DeleteSite(PathCheckingSite site) {
    var conditions = _conditionsIndex[site];
    foreach (var condition in conditions.ToArray()) { // Work on copy, since it may get modified.
      condition.CancelCondition();
    }
    _conditionsIndex.Remove(site);
    _sitesByBlockObject.Remove(site.BlockObject);
    site.DisableComponent();
  }

  #endregion

  #region Profiling tools

  const int StatsTicksThreshold = 20;
  int _tickTillStat = StatsTicksThreshold;
  internal static readonly TicksProfiler PathUpdateProfiler = new();
  static readonly TicksProfiler PathCheckProfiler = new();
  static readonly TicksProfiler WalkersCheckProfiler = new();
  static readonly CounterProfiler SitesProfiler = new();

  void Profile() {
    PathUpdateProfiler.NextFrame();
    PathCheckProfiler.NextFrame();
    WalkersCheckProfiler.NextFrame();
    SitesProfiler.Increment(_sitesByBlockObject.Count);
    SitesProfiler.NextFrame();
    if (--_tickTillStat <= 0) {
      _tickTillStat = StatsTicksThreshold;
      DebugEx.Info("****** PROFILING STATS ******");
      DebugEx.Info("PathUpdate  : {0}", PathUpdateProfiler.GetStatsAndReset());
      DebugEx.Info("PathCheck   : {0}", PathCheckProfiler.GetStatsAndReset());
      DebugEx.Info("WalkersCheck: {0}", WalkersCheckProfiler.GetStatsAndReset());
      DebugEx.Info("Sites       : {0}", SitesProfiler.GetStatsAndReset());
    }
  }

  #endregion

  #region Events

  /// <summary>Drops conditions from the finished objects and marks the path indexes dirty.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnConstructibleEnteredFinishedStateEvent(ConstructibleEnteredFinishedStateEvent @event) {
    TryDeleteSite(@event.Constructible);
  }

  /// <summary>Marks the path indexes dirty.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent @event) {
    TryDeleteSite(@event.Entity);
  }

  #endregion

  #region ISingletonNavMeshListener implementation

  /// <inheritdoc/>
  public void OnNavMeshUpdated(NavMeshUpdate navMeshUpdate) {
    _navMeshIsReady = true;
  }

  #endregion
}

}
