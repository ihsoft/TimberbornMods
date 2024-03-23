// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Automation.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.EntitySystem;
using Timberborn.Navigation;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.WalkingSystem;
using UnityDev.Utils.LogUtilsLite;

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
    if (Features.PathCheckingControllerProfiling) {
      _stopwatch.Start();
    }
    CheckBlockedAccess();
    if (Features.PathCheckingControllerProfiling) {
      _stopwatch.Stop();
      Profile();
    }
  }

  #endregion

  #region API

  /// <summary>Add the path checking condition to monitor.</summary>
  public void AddCondition(CheckAccessBlockCondition condition) {
    var site = PathCheckingSite.GetOrCreate(condition.Behavior.BlockObject);
    _conditionsIndex.GetOrAdd(site).Add(condition);
  }

  /// <summary>Removes the path checking condition from monitor and resets all caches.</summary>
  public void RemoveCondition(CheckAccessBlockCondition condition) {
    if (!PathCheckingSite.SitesByBlockObject.TryGetValue(condition.Behavior.BlockObject, out var site)) {
      DebugEx.Warning("Unknown condition {0} on behavior {1}", condition, condition.Behavior);
      return;
    }
    if (_conditionsIndex.TryGetValue(site, out var valueList)) {
      valueList.Remove(condition);
      if (valueList.Count == 0) {
        _conditionsIndex.Remove(site);
        site.Destroy();
      }
    }
  }

  #endregion

  #region Implementation

  readonly EntityComponentRegistry _entityComponentRegistry;
  readonly NodeIdService _nodeIdService;

  /// <summary>All path checking conditions on the sites.</summary>
  readonly Dictionary<PathCheckingSite, List<CheckAccessBlockCondition>> _conditionsIndex = new();

  /// <summary>Cache of tiles that are paths to the characters on the map.</summary>
  HashSet<int> _walkersTakenNodes;

  /// <summary>Cache of the walking characters positions.</summary>
  /// <remarks>
  /// If a character is at the site being constructed, we don't block since the game checks it naturally.
  /// </remarks>
  HashSet<int> _walkersNodes;

  PathCheckingController(EntityComponentRegistry entityComponentRegistry, AutomationService automationService,
                         NodeIdService nodeIdService) {
    _entityComponentRegistry = entityComponentRegistry;
    _nodeIdService = nodeIdService;
    PathCheckingSite.InjectDependencies(); // FIXME pass the values instead?
    automationService.EventBus.Register(this);
  }

  /// <summary>Sets the condition states based on the path access check.</summary>
  void CheckBlockedAccess() {
    _walkersTakenNodes = null;
    foreach (var indexPair in _conditionsIndex) {
      var site = indexPair.Key;
      var conditions = indexPair.Value;

      // Get it sooner to trigger path creation even on the nonblocking sites.
      site.MaybeUpdateNavMesh();
      if (site.BestBuildersPathNodeIndex.Count == 0) {
        // The unreachable sites cannot get built and trigger the condition.
        UpdateConditions(conditions, false);
        continue;
      }

      // Incomplete sites don't block anything.
      if (site.ConstructionSite.BuildTimeProgress < MaxCompletionProgress) {
        UpdateConditions(conditions, false);
        continue;
      }

      var isBlocked = false;
      // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
      foreach (var testSite in PathCheckingSite.SitesByBlockObject.Values) {
        testSite.MaybeUpdateNavMesh();
        if (ReferenceEquals(testSite, site)
            || !testSite.IsFullyGrounded
            || testSite.BestBuildersPathCornerNodes.Count == 0
            || IsNonBlockingPathSite(site, testSite)) {
          continue;
        }
        isBlocked = true;
        break;
      }
      if (!isBlocked) {
        if (_walkersTakenNodes == null) {
          BuildWalkersIndex();
        }
        isBlocked = site.RestrictedNodes.FastAny(x => _walkersTakenNodes.Contains(x));
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
  static bool IsNonBlockingPathSite(PathCheckingSite pathSite, PathCheckingSite testSite) {
    var testPathNodes = testSite.BestBuildersPathNodeIndex;
    var testPathCorners = testSite.BestBuildersPathCornerNodes;
    var edges = pathSite.NodeEdges;
    for (var i = pathSite.RestrictedNodes.Count - 1; i >= 0; i--) {
      var restrictedCoordinate = pathSite.RestrictedNodes[i];
      if (!testPathNodes.Contains(restrictedCoordinate)) {
        continue;
      }
      var pathPos = testPathCorners.IndexOf(pathSite.SiteNodeId);

      // If the blocking site is at the end of the path, then just check if there is an edge to the test site.
      if (pathPos == testPathCorners.Count - 1) {
        if (edges.FastAny(e => e.Start == restrictedCoordinate && e.End == testSite.BestAccessNode)) {
          continue;
        }
        return false;
      }

      // If the blocking site is in the middle of the path, then verify if the nodes before and after are connected by
      // any edge.
      var nodeAfter = testPathCorners[pathPos + 1];
      var nodeBefore = testPathCorners[pathPos - 1];
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
  void BuildWalkersIndex() {
    _walkersTakenNodes = new HashSet<int>();
    _walkersNodes = new HashSet<int>();
    var walkers = _entityComponentRegistry
        .GetEnabled<BlockOccupant>()
        .Select(x => x.GetComponentFast<Walker>())
        .Where(x => x);
    foreach (var walker in walkers) {
      _walkersNodes.Add(_nodeIdService.WorldToId(walker.TransformFast.position));
      var pathFollower = walker._pathFollower;
      if (pathFollower._pathCorners == null) {
        continue;  // No path, no problem.
      }
      var activePathCorners = pathFollower._pathCorners
          .Skip(pathFollower._nextCornerIndex - 1)
          .Select(_nodeIdService.WorldToId);
      _walkersTakenNodes.AddRange(activePathCorners);
    }
    // Remove nodes that the walkers are currently at due to it's checked by the stock logic.
    foreach (var walkersNode in _walkersNodes) {
      _walkersTakenNodes.Remove(walkersNode);
    }
  }

  /// <summary>Triggers the provided path checking conditions.</summary>
  static void UpdateConditions(List<CheckAccessBlockCondition> conditions, bool isBlocked) {
    foreach (var condition in conditions) {
      condition.ConditionState = !condition.IsReversedCondition ? isBlocked : !isBlocked;
    }
  }

  /// <summary>Tries finding a path checking site for the specified building.</summary>
  bool TryGetSite(BaseComponent component, out PathCheckingSite site) {
    var blockObject = component.GetComponentFast<BlockObject>();
    site = null;
    return blockObject && PathCheckingSite.SitesByBlockObject.TryGetValue(blockObject, out site);
  }

  #endregion

  #region Profiling tools

  readonly Stopwatch _stopwatch = new();
  const int StatsTicksThreshold = 20;

  int _tickTillStat = StatsTicksThreshold;
  int _totalSites;
  int _maxSites;
  int _totalSitesUpdated;
  int _maxSitesUpdated;
  int _sitesUpdatedInTick;
  long _totalStopwatchTicks;
  long _maxStopwatchTicks;

  static string FormatMillis(long stopwatchTicks) {
    return $"{1000f * stopwatchTicks / Stopwatch.Frequency:0.###}ms";
  }

  void Profile() {
    _maxStopwatchTicks = Math.Max(_maxStopwatchTicks, _stopwatch.ElapsedTicks);
    _totalStopwatchTicks += _maxStopwatchTicks;
    _totalSites += _conditionsIndex.Count;
    _maxSites = Math.Max(_maxSites, _conditionsIndex.Count);
    _maxSitesUpdated = Math.Max(_maxSitesUpdated, _sitesUpdatedInTick);
    _totalSitesUpdated += _sitesUpdatedInTick;
    _sitesUpdatedInTick = 0;
    if (--_tickTillStat <= 0) {
      _tickTillStat = StatsTicksThreshold;
      var info = new StringBuilder();
      info.AppendFormat("**** Stats for PathCheckingController, {0} ticks window:\n", StatsTicksThreshold);
      info.AppendFormat(
          "Cost: avg={0}, total={1}, max={2}\n", FormatMillis(_totalStopwatchTicks / StatsTicksThreshold),
          FormatMillis(_totalStopwatchTicks), FormatMillis(_maxStopwatchTicks));
      info.AppendFormat("Sites: avg={0:0.##}, max={1}\n", (float)_totalSites / StatsTicksThreshold, _maxSites);
      info.AppendFormat("Updates: avg={0}, total={1}, max={2}\n",
                        (float)_totalSitesUpdated / StatsTicksThreshold, _totalSitesUpdated, _maxSitesUpdated);
      DebugEx.Info(info.ToString());
      _totalSites = 0;
      _maxSites = 0;
      _totalSitesUpdated = 0;
      _maxSitesUpdated = 0;
      _totalStopwatchTicks = 0;
      _maxStopwatchTicks = 0;
      _stopwatch.Reset();
    }
  }

  #endregion

  #region ISingletonNavMeshListener implemenation

  /// <inheritdoc/>
  public void OnNavMeshUpdated(NavMeshUpdate navMeshUpdate) {
    foreach (var site in PathCheckingSite.SitesByBlockObject.Values) {
      site.OnNavMeshUpdate(navMeshUpdate);
      if (site.NeedsBestPathUpdate) {
        _sitesUpdatedInTick++;
      }
    }
  }

  #endregion

  #region Events

  /// <summary>Drops conditions from the finished objects and marks the path indexes dirty.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnConstructibleEnteredFinishedStateEvent(ConstructibleEnteredFinishedStateEvent @event) {
    if (!TryGetSite(@event.Constructible, out var site)) {
      return;
    }
    var conditions = _conditionsIndex[site];
    foreach (var condition in conditions.ToArray()) {  // Work on copy, since it may get modified.
      if (condition.Behavior) {
        condition.CancelCondition();
      }
    }
    _conditionsIndex.Remove(site);
    site.Destroy();
    foreach (var updateSite in PathCheckingSite.SitesByBlockObject.Values) {
      updateSite.OnConstructibleCompleted(@event.Constructible);
      if (updateSite.NeedsBestPathUpdate) {
        _sitesUpdatedInTick++;
      }
    }
  }

  /// <summary>Marks the path indexes dirty.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent @event) {
    if (!TryGetSite(@event.Entity, out var site)) {
      return;
    }
    _conditionsIndex.Remove(site);
    site.Destroy();
  }

  #endregion
}

}
