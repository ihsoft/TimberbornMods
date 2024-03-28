// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlockSystemNavigation;
using Timberborn.BuilderHubSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.BuildingsNavigation;
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
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace Automation.PathCheckingSystem {

/// <summary>Container for the path blocking site.</summary>
/// <remarks>It must only be applied to the preview sites.</remarks>
sealed class PathCheckingSite : BaseComponent, ISelectionListener, INavMeshListener, IFinishedStateListener,
                                IDeletableEntity, IInitializableEntity {

  #region API
  // ReSharper disable MemberCanBePrivate.Global

  /// <summary>Tells whether this site can be finished without blocking other buildings.</summary>
  /// <seealso cref="PathCheckingService.CheckBlockingStateAndTriggerActions"/>
  public bool CanFinish { get; internal set; }

  /// <summary>Site's NavMesh node ID.</summary>
  public int SiteNodeId { get; private set; }

  /// <summary>Construction site cached instance.</summary>
  // FIXME: It's temporary! Use site blockers instead.
  public ConstructionSite ConstructionSite { get; private set; }

  /// <summary>BlockObject of this site.</summary>
  public BlockObject BlockObject { get; private set; }

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
  public int BestAccessNode { get; private set; } = -1;

  /// <summary>Coordinates of the positions that are taken by the site.</summary>
  public List<int> RestrictedNodes { get; private set; }

  /// <summary>Indicates that the site _may_ become reachable when all the preview buildings are built.</summary>
  /// <remarks>
  /// It's a best effort check. There is no guarantee the preview buildings are actually providing access. The
  /// <see cref="BestBuildersPathCornerNodes"/> can be absent for such sites.
  /// </remarks>
  public bool CanBeAccessedInPreview { get; private set; }

  /// <summary>Makes the component active on the game object. This resumes all side effects.</summary>
  /// <remarks>
  /// Only call it when re-suing an existing component. All new components, including the loaded ones, should get
  /// initialized via <see cref="InitializeEntity"/>.
  /// </remarks>
  /// <seealso cref="DisableComponent"/>
  public void EnableComponent() {
    _eventBus.Register(this);
    _navMeshListenerEntityRegistry.RegisterNavMeshListener(this);
    enabled = true;
    MaybeUpdateNavMesh();
  }

  /// <summary>Disable the component and clan up all its side effects.</summary>
  /// <remarks>
  /// Once added to the game object, the component must not be destroyed. If the site is added to the building again,
  /// the existing component must be re-used.
  /// </remarks>
  /// <seealso cref="EnableComponent"/>
  public void DisableComponent() {
    ClearAllStates();
    ResetState();
    _eventBus.Unregister(this);
    _navMeshListenerEntityRegistry.UnregisterNavMeshListener(this);
    enabled = false;
  }

  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region Profiling support

  #endregion

  #region Implementation

  const string NotYetReachableLocKey = "IgorZ.Automation.CheckAccessBlockCondition.NotYetReachable";
  const string UnreachableStatusLocKey = "IgorZ.Automation.CheckAccessBlockCondition.UnreachableStatus";
  const string UnreachableAlertLocKey = "IgorZ.Automation.CheckAccessBlockCondition.UnreachableAlert";
  const string UnreachableIconName = "UnreachableObject";

  ILoc _loc;
  NodeIdService _nodeIdService;
  DistrictCenterRegistry _districtCenterRegistry;
  DistrictMap _districtMap;
  PreviewDistrictMap _previewDistrictMap;
  INavigationService _navigationService;
  EventBus _eventBus;
  NavMeshListenerEntityRegistry _navMeshListenerEntityRegistry;

  BlockableBuilding _blockableBuilding;
  BuilderJobReachabilityStatus _builderJobReachabilityStatus;
  GroundedConstructionSite _groundedSite;
  Accessible _accessible;
  BlockObjectNavMesh _blockObjectNavMesh;

  StatusToggle _unreachableStatusToggle;
  StatusToggle _maybeReachableStatusToggle;
  int _bestPathRoadNodeId = -1;
  bool _isFullyGrounded;
  bool _isCurrentlySelected;

  /// <summary>It is called before <see cref="InjectDependencies"/>!</summary>
  void Awake() {
    BlockObject = GetComponentFast<BlockObject>();
    if (BlockObject.Preview) {
      throw new InvalidOperationException($"{DebugEx.BaseComponentToString(BlockObject)} must be in preview");
    }
    ConstructionSite = GetComponentFast<ConstructionSite>();
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _builderJobReachabilityStatus = GetComponentFast<BuilderJobReachabilityStatus>();
    _groundedSite = GetComponentFast<GroundedConstructionSite>();
    _accessible = GetComponentFast<ConstructionSiteAccessible>().Accessible;
    _blockObjectNavMesh = GetComponentFast<BlockObjectNavMesh>();
  }

  /// <summary>It is called after <see cref="Awake"/>!</summary>
  /// <remarks>It must be public to work.</remarks>
  [Inject]
  public void InjectDependencies(
      ILoc loc, NodeIdService nodeIdService, DistrictCenterRegistry districtCenterRegistry, DistrictMap districtMap,
      PreviewDistrictMap previewDistrictMap, INavigationService navigationService, EventBus eventBus,
      NavMeshListenerEntityRegistry navMeshListenerEntityRegistry) {
    _loc = loc;
    _nodeIdService = nodeIdService;
    _districtCenterRegistry = districtCenterRegistry;
    _districtMap = districtMap;
    _previewDistrictMap = previewDistrictMap;
    _navigationService = navigationService;
    _eventBus = eventBus;
    _navMeshListenerEntityRegistry = navMeshListenerEntityRegistry;
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

  /// <summary>Updates the properties that depend on the district's NavMesh.</summary>
  /// <remarks>
  /// The site must be within the range from the road, which is currently 9 tiles. We intentionally ignore the accesses
  /// below the site level: even though the game can construct sites this way (one tile above the road), the path
  /// checking algo cannot deal with all the corners cases. It may limit players in their construction methods, but
  /// that's the price to pay.
  /// </remarks>
  public void MaybeUpdateNavMesh() {
    if (!_isFullyGrounded) {
      return;  // If not grounded, then it cannot start building.
    }
    PathCheckingService.PathUpdateProfiler.StartNewHit();
    if (RestrictedNodes == null) {
      InitializeNavMesh();
    }
    ResetState();

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
      ClearAllStates();
    } else {
      if (CanBeAccessedInPreview) {
        SetMaybeReachableStatus();
      } else {
        SetUnreachableStatus();
      }
    }
    PathCheckingService.PathUpdateProfiler.Stop();
  }

  /// <summary>Blocks the site since there is no controllable ways to reach it.</summary>
  /// <remarks>
  /// The site can actually be reachable for the stock game, but the algo may not be aware of it. So, just block the
  /// site to not get unexpected blocks in the other chains.
  /// </remarks>
  void SetUnreachableStatus() {
    _unreachableStatusToggle.Activate();
    _maybeReachableStatusToggle.Deactivate();
    _blockableBuilding.Block(this);
  }

  /// <summary>Blocks site, but indicates that there can be path being built.</summary>
  void SetMaybeReachableStatus() {
    _unreachableStatusToggle.Deactivate();
    _maybeReachableStatusToggle.Activate();
    _blockableBuilding.Block(this);
  }

  /// <summary>A cleanup method that resets all effects on the site and resumes the stock one.</summary>
  void ClearAllStates() {
    _unreachableStatusToggle.Deactivate();
    _maybeReachableStatusToggle.Deactivate();
    _blockableBuilding.Unblock(this);
    if (_isCurrentlySelected && _builderJobReachabilityStatus) {
      _builderJobReachabilityStatus.OnSelect();
    }
  }

  /// <summary>Reset to unreachable site.</summary>
  void ResetState() {
    BestBuildersPathNodeIndex = new HashSet<int>();
    BestBuildersPathCornerNodes = new List<int>();
    BestAccessNode = -1;
    _bestPathRoadNodeId = -1;
    CanBeAccessedInPreview = false;
  }

  #endregion

  #region Events for the state update

  /// <summary>Reacts on construction complete and verifies if a non-grounded site became grounded.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnConstructibleEnteredFinishedStateEvent(ConstructibleEnteredFinishedStateEvent @event) {
    if (!_groundedSite.IsFullyGrounded || !enabled) {
      return;
    }
    if (@event.Constructible.GetComponentFast<BlockObject>() == BlockObject) {
      return;  // On self-construction the site will be removed.
    }
    _isFullyGrounded = true;
    MaybeUpdateNavMesh();
  }

  /// <summary>Deleted entities can unblock unreachable sites.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent @event) {
    if (BestAccessNode == -1 && @event.Entity.GetComponentFast<BlockObject>() != BlockObject) {
      MaybeUpdateNavMesh();
    }
  }

  #endregion

  #region ISingletonNavMeshListener implemenation

  /// <inheritdoc/>
  public void OnNavMeshUpdated(NavMeshUpdate navMeshUpdate) {
    if (!_isFullyGrounded || !enabled) {
      return;
    }
    if (_bestPathRoadNodeId == -1) {
      // For an unreachable site, _any_ update to navmesh can make it reachable.
      // Also, the road doesn't exists on the site were loaded just loaded.
      MaybeUpdateNavMesh();
      return;
    }
    // If we have a path, then check if the navmesh change affects at least one of the nodes. No change, no problem.
    if (navMeshUpdate.RoadNodeIds.FastContains(_bestPathRoadNodeId)
        || navMeshUpdate.TerrainNodeIds.FastAny(BestBuildersPathNodeIndex.Contains)) {
      MaybeUpdateNavMesh();
    }
  }

  #endregion

  #region ISelectionListener implemenation

  /// <inheritdoc/>
  public void OnSelect() {
    _isCurrentlySelected = true;
    if (!_builderJobReachabilityStatus) {
      return;
    }
    if (_unreachableStatusToggle.IsActive || _maybeReachableStatusToggle.IsActive) {
      _builderJobReachabilityStatus.OnUnselect();  // We show our own status.
    }
  }

  /// <inheritdoc/>
  public void OnUnselect() {
    _isCurrentlySelected = false;
  }

  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    DisableComponent();
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {}

  #endregion

  #region IDeletableEntity implementation

  /// <inheritdoc/>
  public void DeleteEntity() {
    DisableComponent();
  }

  #endregion

  #region IInitializableEntity

  /// <inheritdoc/>
  public void InitializeEntity() {
    if (_unreachableStatusToggle != null) {
      return;  // Already initialized.
    }
    _unreachableStatusToggle = StatusToggle.CreatePriorityStatusWithAlertAndFloatingIcon(
        UnreachableIconName, _loc.T(UnreachableStatusLocKey), _loc.T(UnreachableAlertLocKey));
    _maybeReachableStatusToggle = StatusToggle.CreateNormalStatus(UnreachableIconName, _loc.T(NotYetReachableLocKey));
    GetComponentFast<StatusSubject>().RegisterStatus(_unreachableStatusToggle);
    GetComponentFast<StatusSubject>().RegisterStatus(_maybeReachableStatusToggle);
    _isFullyGrounded = _groundedSite.IsFullyGrounded;
    EnableComponent();
  }

  #endregion
}

}
