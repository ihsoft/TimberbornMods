// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using TimberApi.DependencyContainerSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlockSystemNavigation;
using Timberborn.BuildingsNavigation;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.ConstructionSites;
using Timberborn.Coordinates;
using Timberborn.GameDistricts;
using Timberborn.Localization;
using Timberborn.Navigation;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace Automation.PathCheckingSystem {

/// <summary>Container for the path blocking site.</summary>
sealed class PathCheckingSite {
  #region API
  // ReSharper disable MemberCanBePrivate.Global

  /// <summary>Site's NavMesh node ID.</summary>
  public int SiteNodeId { get; private set; }

  /// <summary>Construction site cached instance.</summary>
  // FIXME: It's temporary! Use site blockers instead.
  public readonly ConstructionSite ConstructionSite;

  /// <summary>BlockObject of this site.</summary>
  public readonly BlockObject BlockObject;

  /// <summary>Tells if all the site's foundation blocks stay on top fo finished entities.</summary>
  public bool IsFullyGrounded => _groundedSite.IsFullyGrounded;

  /// <summary>NavMesh nodes version of the stock <see cref="NavMeshEdge"/>.</summary>
  public struct NodeEdge {
    public int Start;
    public int End;
  }

  /// <summary>Path edges from the site if there are any.</summary>
  public List<NodeEdge> NodeEdges { get; private set; }

  /// <summary>The path from the closest road to the closets construction site's accessible.</summary>
  /// <seealso cref="MaybeUpdateNavMesh"/>
  public List<int> BestBuildersPathCornerNodes { get; private set; }

  /// <summary>The best path index. If it's empty, then the site cannot be reached.</summary>
  /// <seealso cref="CanBeAccessedInPreview"/>
  /// <seealso cref="MaybeUpdateNavMesh"/>
  /// <seealso cref="BestBuildersPathCornerNodes"/>
  public HashSet<int> BestBuildersPathNodeIndex { get; private set; }

  /// <summary>The access that was used to build <see cref="BestBuildersPathCornerNodes"/>.</summary>
  public int BestAccessNode { get; private set; }

  /// <summary>Coordinates of the positions that are taken by the site.</summary>
  public List<int> RestrictedNodes { get; private set; }

  /// <summary>Indicates that all the fields related to path and NavMesh are invalid and need to be updated.</summary>
  /// <seealso cref="MaybeUpdateNavMesh"/>
  public bool NeedsBestPathUpdate => BestBuildersPathNodeIndex == null;

  /// <summary>Indicates that the site _may_ become reachable when all the preview buildings are built.</summary>
  /// <remarks>
  /// It's a best effort check. There is no guarantee the preview buildings are actually providing access. The
  /// <see cref="BestBuildersPathCornerNodes"/> can be absent for such sites.
  /// </remarks>
  public bool CanBeAccessedInPreview { get; private set; }

  /// <summary>Drops the site and all internal caches associated with it.</summary>
  public void Destroy() {
    _statusTracker.Cleanup();
  }

  /// <summary>Verifies that the all NavMesh related things are up to date on the site.</summary>
  /// <remarks>If no updates needed, it is a very cheap call.</remarks>
  /// <seealso cref="NeedsBestPathUpdate"/>
  /// <seealso cref="BestBuildersPathNodeIndex"/>
  /// <seealso cref="BestBuildersPathCornerNodes"/>
  public void MaybeUpdateNavMesh() {
    if (NeedsBestPathUpdate) {
      UpdateBestPath();
    }
  }

  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region Implementation

  static BaseInstantiator _baseInstantiator;
  static ILoc _loc;
  static NodeIdService _nodeIdService;
  static DistrictCenterRegistry _districtCenterRegistry;
  static DistrictMap _districtMap;
  static PreviewDistrictMap _previewDistrictMap;
  static INavigationService _navigationService;

  readonly Accessible _accessible;
  readonly PathCheckingSiteStatusTracker _statusTracker;
  readonly GroundedConstructionSite _groundedSite;
  readonly BlockObjectNavMesh _blockObjectNavMesh;

  int _bestPathRoadNodeId = -1;

  /// <exception cref="InvalidOperationException"> if the site doesn't have all teh expected components.</exception>
  public PathCheckingSite(BlockObject blockObject) {
    BlockObject = blockObject;
    ConstructionSite = BlockObject.GetComponentFast<ConstructionSite>();
    _groundedSite = BlockObject.GetComponentFast<GroundedConstructionSite>();
    _accessible = BlockObject.GetComponentFast<ConstructionSiteAccessible>().Accessible;
    _blockObjectNavMesh = BlockObject.GetComponentFast<BlockObjectNavMesh>();
    _statusTracker = BlockObject.GetComponentFast<PathCheckingSiteStatusTracker>();
    if (!_statusTracker) {
      _statusTracker = _baseInstantiator.AddComponent<PathCheckingSiteStatusTracker>(BlockObject.GameObjectFast);
      _statusTracker.Initialize(_loc);
    }
    if (!ConstructionSite || !_groundedSite || !_accessible) {
      throw new InvalidOperationException(
          $"{DebugEx.BaseComponentToString(BlockObject)} is not a valid construction site");
    }
  }

  /// <summary>Initializes the NavMesh related things.</summary>
  /// <remarks>This must be done on an object hat is already added to the game's NavMesh.</remarks>
  void InitializeNavMesh() {
    SiteNodeId = _nodeIdService.GridToId(BlockObject.Coordinates);
    var navMeshObject = _blockObjectNavMesh.NavMeshObject;
    RestrictedNodes = navMeshObject._restrictedCoordinates.Select(_nodeIdService.GridToId).ToList();
    NodeEdges = navMeshObject._addingChanges
        .Where(x => x.NavMeshChangeType == NavMeshChangeType.AddEdge)
        .Select(x => new NodeEdge {
            Start = _nodeIdService.GridToId(x.NavMeshEdge.Start),
            End = _nodeIdService.GridToId(x.NavMeshEdge.End),
        })
        .ToList();
  }

  /// <summary>Gets all the needed injections from the dependency container.</summary>
  internal static void InjectDependencies() {
    _baseInstantiator = DependencyContainer.GetInstance<BaseInstantiator>();
    _loc = DependencyContainer.GetInstance<ILoc>();
    _nodeIdService = DependencyContainer.GetInstance<NodeIdService>();
    _districtCenterRegistry = DependencyContainer.GetInstance<DistrictCenterRegistry>();
    _districtMap = DependencyContainer.GetInstance<DistrictMap>();
    _previewDistrictMap = DependencyContainer.GetInstance<PreviewDistrictMap>();
    _navigationService = DependencyContainer.GetInstance<INavigationService>();
  }

  /// <summary>
  /// Resets <see cref="BestBuildersPathNodeIndex"/> and <see cref="BestBuildersPathCornerNodes"/> to trigger the path
  /// rebuild.
  /// </summary>
  void RequestBestPathUpdate() {
    BestBuildersPathNodeIndex = null;
    BestBuildersPathCornerNodes = null;
  }

  /// <summary>Updates the <see cref="BestBuildersPathNodeIndex"/> to the site from the closets road node.</summary>
  /// <remarks>
  /// The site must be within the range from the road, which is currently 9 tiles. We intentionally ignore the accesses
  /// below the site level: even though the game can construct sites this way (one tile above the road), the path
  /// checking algo cannot deal with all the corners cases. It may limit players in their construction methods, but
  /// that's the price to pay.
  /// </remarks>
  void UpdateBestPath() {
    if (RestrictedNodes == null) {
      InitializeNavMesh();
    }
    BestBuildersPathNodeIndex = new HashSet<int>();
    BestBuildersPathCornerNodes = new List<int>();
    _bestPathRoadNodeId = -1;
    CanBeAccessedInPreview = false;
    if (!_groundedSite.IsFullyGrounded) {
      return;
    }
    if (!_accessible.enabled) {
      HostedDebugLog.Error(ConstructionSite, "Disabled accessible in use");
      return;
    }

    var bestDistance = float.MaxValue;
    var bestRoadNode = -1;
    var bestAccess = Vector3.zero;
    var minAccessHeight = CoordinateSystem.GridToWorld(BlockObject.Coordinates).y;
    var accesses = _accessible.Accesses
        .Where(access => access.y >= minAccessHeight && _nodeIdService.Contains(access));
    foreach (var access in accesses) {
      var accessNode = _nodeIdService.WorldToId(access);
      foreach (var district in _districtCenterRegistry.AllDistrictCenters) {
        var flow = _districtMap.GetDistrictRoadSpillFlowField(district.District);
        if (!flow.HasNode(accessNode)) {
          if (!CanBeAccessedInPreview) {
            var previewFlow = _previewDistrictMap.GetDistrictRoadSpillFlowField(district.District);
            CanBeAccessedInPreview = previewFlow.HasNode(accessNode);
          }
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
    if (bestRoadNode != -1) {
      var bestRoadPosition = _nodeIdService.IdToWorld(bestRoadNode);
      var pathCorners = new List<Vector3>();
      _navigationService.FindPathUnlimitedRange(bestRoadPosition, bestAccess, pathCorners, out _);
      BestAccessNode = _nodeIdService.WorldToId(bestAccess);
      BestBuildersPathCornerNodes = pathCorners.Select(_nodeIdService.WorldToId).ToList();
      BestBuildersPathNodeIndex = pathCorners.Select(_nodeIdService.WorldToId).ToHashSet();
      _bestPathRoadNodeId = bestRoadNode;
      _statusTracker.Cleanup();
    } else {
      if (CanBeAccessedInPreview) {
        _statusTracker.SetMaybeReachable();
      } else {
        _statusTracker.SetUnreachable();
      }
    }
  }

  #endregion

  #region Callbacks for the state update

  /// <summary>It's expected to be called from <see cref="PathCheckingService"/> when the navmesh changes.</summary>
  /// <remarks>Don't do any logic here! Only mark the state invalid to get updated in the next tick.</remarks>
  internal void OnNavMeshUpdate(NavMeshUpdate navMeshUpdate) {
    if (!_groundedSite.IsFullyGrounded) {
      return;  // The ungrounded site just cannot start building.
    }
    if (_bestPathRoadNodeId == -1) {
      RequestBestPathUpdate();  // For an unreachable site, _any_ update to navmesh can make it reachable.
      return;
    }
    // Otherwise, if the navmesh change affects at least one of the road nodes of the site, then it's a trigger.
    if (navMeshUpdate.RoadNodeIds.FastContains(_bestPathRoadNodeId)
        || navMeshUpdate.TerrainNodeIds.FastAny(BestBuildersPathNodeIndex.Contains)) {
      RequestBestPathUpdate();
    }
  }

  /// <summary>Reacts on construction complete and verifies if a non-grounded site can now start building.</summary>
  internal void OnConstructibleCompleted(Constructible constructible) {
    if (_groundedSite.IsFullyGrounded && _bestPathRoadNodeId == -1) {
      RequestBestPathUpdate();
    }
  }

  #endregion
}

}
