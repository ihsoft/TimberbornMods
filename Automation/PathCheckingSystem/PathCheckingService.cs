// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.Settings;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Navigation;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.PathCheckingSystem;

/// <summary>The component that handles all path checking conditions.</summary>
/// <remarks>
/// It can't be handled in the scope of one condition due to all of them are interconnected (they can affect each
/// other). This controller has "the full picture" and orchestrates all the conditions.
/// </remarks>
sealed class PathCheckingService : ITickableSingleton {

  #region ITickableSingleton implementation

  /// <inheritdoc/>
  public void Tick() {
    if (AutomationDebugSettings.PathCheckingSystemProfiling) {
      Profile();
    }
  }

  #endregion

  #region API

  /// <summary>Shortcut for the Harmony patches.</summary>
  /// <remarks>All normal classes should inject the service.</remarks>
  internal static PathCheckingService Instance;

  /// <summary>Total number of the sites under system control.</summary>
  internal int NumberOfSites => _conditionsIndex.Count;

  /// <summary>Timer for the debugger panel.</summary>
  internal static readonly Stopwatch PatchCheckingTimer = new();

  /// <summary>Add the path checking condition to monitor.</summary>
  public void AddCondition(CheckAccessBlockCondition condition) {
    var site = GetOrCreateSite(condition.Behavior);
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
  readonly DistrictMap _districtMap;

  /// <summary>All path checking conditions on the sites.</summary>
  readonly Dictionary<PathCheckingSite, List<CheckAccessBlockCondition>> _conditionsIndex = new();

  /// <summary>All path sites indexed by blockobject.</summary>
  readonly Dictionary<BlockObject, PathCheckingSite> _sitesByBlockObject = new();

  /// <summary>Cache of tiles that are paths to the characters on the map.</summary>
  HashSet<int> _walkersTakenNodes;

  PathCheckingService(EntityComponentRegistry entityComponentRegistry, AutomationService automationService,
                      NodeIdService nodeIdService, DistrictMap districtMap) {
    Instance = this;
    _entityComponentRegistry = entityComponentRegistry;
    _nodeIdService = nodeIdService;
    _districtMap = districtMap;
    automationService.EventBus.Register(this);
  }

  /// <summary>
  /// Runs the checks to update <see cref="PathCheckingSite.CanFinish"/> and triggers the relevant conditions.
  /// </summary>
  public void CheckBlockingStateAndTriggerActions(PathCheckingSite site) {
    PatchCheckingTimer.Start();
    var newCanFinish = !IsBlockingSite(site);
    if (site.CanFinish == newCanFinish) {
      PatchCheckingTimer.Stop();
      return;  // No need to trigger the condition.
    }
    site.CanFinish = newCanFinish;
    var conditions = _conditionsIndex[site];
    foreach (var condition in conditions) {
      condition.ConditionState = condition.IsReversedCondition ? newCanFinish : !newCanFinish;
    }
    PatchCheckingTimer.Stop();
  }

  /// <summary>Determines if the site construction can complete without obstructing any other site.</summary>
  bool IsBlockingSite(PathCheckingSite site) {
    if (site.BestAccessNode == -1 || site.RestrictedNodes.Count == 0) {
      return false;  // This site can't block anything.
    }
    PathCheckProfiler.StartNewHit();
    var isBlocked = false;
    site.BlockedSite = null;
    foreach (var testSite in _sitesByBlockObject.Values) {
      if (testSite.BestAccessNode != -1 && !ReferenceEquals(testSite, site) && !IsNonBlockingPathSite(site, testSite)) {
        site.BlockedSite = testSite;
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
      var pathPos = testPathCorners.IndexOf(restrictedCoordinate);

      // 1. If the path site is in the middle of the test path, then verify if the nodes before and after are connected
      //    by any edge.
      // 2. If the path site is at the end of the test path, then it's staying at the road. Skip road-to-path
      //    connectivity check.
      // 3. If the path site is at the start of the test path, then check if it has an edge to the access to ensure the
      //    builders can pass.  
      var nodeAfter = pathPos > 0 ? testPathCorners[pathPos - 1] : testSite.BestAccessNode;
      var nodeBefore = pathPos < testPathCorners.Count - 1 ? testPathCorners[pathPos + 1] : -1;
      var isConnectedToTestSite = false;
      var isConnectedToTheTestPath = nodeBefore == -1;  // Path site can stay at the road.
      // FIXME: error prone checking. Need to consider height change within the path site.
      for (var j = edges.Count - 1; j >= 0; j--) {
        var edge = edges[j];
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

  /// <summary>Gathers all coordinates that are taken by the character paths.</summary>
  /// <remarks>We don't want to let the builders get stranded.</remarks>
  void MaybeBuildWalkersIndex() {
    if (_lastCacheBuiltFrame == Time.frameCount) {
      return; // The cache is up to date.
    }
    WalkersCheckProfiler.StartNewHit();
    _lastCacheBuiltFrame = Time.frameCount;
    _walkersTakenNodes = new HashSet<int>();
    var citizens = _entityComponentRegistry.GetEnabled<BlockOccupant>()
        .Select(x => x.GetComponent<Citizen>())
        .Where(x => x && x.HasAssignedDistrict);
    foreach (var citizen in citizens) {
      var pathNodes = GetPathToRoadFast(citizen.Transform.position, citizen.AssignedDistrict.District);
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
    var blockObject = component.GetComponent<BlockObject>();
    if (blockObject && _sitesByBlockObject.TryGetValue(blockObject, out var site)) {
      DeleteSite(site);
    }
  }

  /// <summary>Finds the existing construction site or creates a new one.</summary>
  PathCheckingSite GetOrCreateSite(AutomationBehavior automationBehavior) {
    var blockObject = automationBehavior.BlockObject;
    if (!_sitesByBlockObject.TryGetValue(blockObject, out var cachedSite)) {
      cachedSite = automationBehavior.GetOrCreate<PathCheckingSite>();
      if (!cachedSite.Enabled) {
        cachedSite.EnableSiteComponent();
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
    site.DisableSiteComponent();
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
  public void OnBlockObjectEnteredFinishedStateEvent(EnteredFinishedStateEvent e) {
    TryDeleteSite(e.BlockObject);
  }

  /// <summary>Marks the path indexes dirty.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent @event) {
    TryDeleteSite(@event.Entity);
  }

  /// <summary>Propagates selection events to the sites.</summary>
  /// <remarks>Dynamic components only get a limited set of BaseComponent events.</remarks>
  /// <seealso cref="AbstractDynamicComponent"/>
  [OnEvent]
  public void OnSelectableObjectSelectedEvent(SelectableObjectSelectedEvent @event) {
    var checkObject = @event.SelectableObject.GameObject;
    var targetSite = _sitesByBlockObject.Values.FirstOrDefault(x => x.AutomationBehavior.GameObject == checkObject);
    targetSite?.OnSelect();
  }

  /// <summary>Propagates selection events to the sites.</summary>
  /// <remarks>Dynamic components only get a limited set of BaseComponent events.</remarks>
  /// <seealso cref="AbstractDynamicComponent"/>
  [OnEvent]
  public void OnSelectableObjectUnselectedEvent(SelectableObjectUnselectedEvent @event) {
    var checkObject = @event.SelectableObject.GameObject;
    var targetSite = _sitesByBlockObject.Values.FirstOrDefault(x => x.AutomationBehavior.GameObject == checkObject);
    targetSite?.OnUnselect();
  }

  #endregion
}