// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockingSystem;
using Timberborn.BlockSystem;
using Timberborn.BlockSystemNavigation;
using Timberborn.BuildingsNavigation;
using Timberborn.BuildingsReachability;
using Timberborn.Common;
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

namespace IgorZ.Automation.PathCheckingSystem;

/// <summary>Container for the path blocking site.</summary>
/// <remarks>It must only be applied to the preview sites.</remarks>
sealed class PathCheckingSite : AbstractDynamicComponent, ISelectionListener, INavMeshListener, IFinishedStateListener,
                                IInitializableEntity, IAwakableComponent {

  #region API
  // ReSharper disable MemberCanBePrivate.Global

  /// <summary>Timer for the debugger panel.</summary>
  internal static readonly Stopwatch NavMeshUpdateTimer = new();

  /// <summary>Tells whether this site can be finished without blocking other buildings.</summary>
  /// <seealso cref="PathCheckingService.CheckBlockingStateAndTriggerActions"/>
  public bool CanFinish { get; internal set; } = true;

  /// <summary>The other site that blocks this one.</summary>
  public PathCheckingSite BlockedSite { get; internal set; }

  /// <summary>Construction site cached instance.</summary>
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

  /// <summary>The path from the  construction site's accessible to the closest road.</summary>
  /// <remarks>It's a directional list. It starts at the accessible coordinate and goes to the road.</remarks>
  /// <seealso cref="UpdateNavMesh"/>
  public List<int> BestBuildersPathCornerNodes { get; private set; }

  /// <summary>The best path index. If it's empty, then the site cannot be reached.</summary>
  /// <seealso cref="CanBeAccessedInPreview"/>
  /// <seealso cref="UpdateNavMesh"/>
  /// <seealso cref="BestBuildersPathCornerNodes"/>
  public HashSet<int> BestBuildersPathNodeIndex { get; private set; }

  /// <summary>The access that was used to build <see cref="BestBuildersPathCornerNodes"/>.</summary>
  public int BestAccessNode { get; private set; } = -1;

  /// <summary>Coordinates of the positions that are taken by the site.</summary>
  public List<int> RestrictedNodes { get; private set; }

  /// <summary>Indicates that the site _may_ become reachable when all the preview buildings are built.</summary>
  /// <remarks>
  /// It's the best effort check. There is no guarantee the preview buildings are actually providing access. The
  /// <see cref="BestBuildersPathCornerNodes"/> can be absent for such sites.
  /// </remarks>
  public bool CanBeAccessedInPreview { get; private set; }

  /// <summary>Makes the component active on the game object. This resumes all side effects.</summary>
  /// <remarks>
  /// Only call it when re-using an existing component. All new components, including the loaded ones, should get
  /// initialized via <see cref="InitializeEntity"/>.
  /// </remarks>
  /// <seealso cref="DisableSiteComponent"/>
  public void EnableSiteComponent() {
    _eventBus.Register(this);
    _navMeshListenerEntityRegistry.RegisterNavMeshListener(this);
    if (_isValid) {
      UpdateNavMesh();
    }
    EnableComponent();
  }

  /// <summary>Disable the component and clan up all its side effects.</summary>
  /// <remarks>
  /// Once added to the game object, the component must not be destroyed. If the site is added to the building again,
  /// the existing component must be re-used.
  /// </remarks>
  public void DisableSiteComponent() {
    ClearAllStates();
    ResetState();
    _eventBus.Unregister(this);
    _navMeshListenerEntityRegistry.UnregisterNavMeshListener(this);
    DisableComponent();
  }

  const string NotYetReachableLocKey = "IgorZ.Automation.CheckAccessBlockCondition.NotYetReachable";
  const string UnreachableStatusLocKey = "IgorZ.Automation.CheckAccessBlockCondition.UnreachableStatus";
  const string UnreachableAlertLocKey = "IgorZ.Automation.CheckAccessBlockCondition.UnreachableAlert";
  const string UnreachableIconName = "UnreachableObject";

  ILoc _loc;
  NodeIdService _nodeIdService;
  DistrictCenterRegistry _districtCenterRegistry;
  DistrictMap _districtMap;
  PreviewDistrictMap _previewDistrictMap;
  EventBus _eventBus;
  INavMeshListenerEntityRegistry _navMeshListenerEntityRegistry;

  BlockableObject _blockableObject;
  EntityReachabilityStatus _entityReachabilityStatus;
  GroundedConstructionSite _groundedSite;
  Accessible _accessible;
  BlockObjectNavMesh _blockObjectNavMesh;

  StatusToggle _unreachableStatusToggle;
  StatusToggle _maybeReachableStatusToggle;
  int _bestPathRoadNodeId = -1;
  bool _isValid;
  bool _isCurrentlySelected;

  /// <summary>It is called after <see cref="Awake"/>!</summary>
  /// <remarks>It must be public to work.</remarks>
  [Inject]
  public void InjectDependencies(
      ILoc loc, NodeIdService nodeIdService, DistrictCenterRegistry districtCenterRegistry, DistrictMap districtMap,
      PreviewDistrictMap previewDistrictMap, EventBus eventBus,
      INavMeshListenerEntityRegistry navMeshListenerEntityRegistry) {
    _loc = loc;
    _nodeIdService = nodeIdService;
    _districtCenterRegistry = districtCenterRegistry;
    _districtMap = districtMap;
    _previewDistrictMap = previewDistrictMap;
    _eventBus = eventBus;
    _navMeshListenerEntityRegistry = navMeshListenerEntityRegistry;
  }

  /// <summary>Initializes the NavMesh related things.</summary>
  /// <remarks>This must be done on an object that is already added to the game's NavMesh.</remarks>
  void InitializeNavMesh() {
    var navMeshObject = _blockObjectNavMesh.NavMeshObject;
    RestrictedNodes = navMeshObject._restrictedCoordinates.Select(_nodeIdService.GridToId).ToList();
    var settings = BlockObject.GetComponent<BlockObjectNavMeshSettingsSpec>();
    if (settings == null) {
      NodeEdges = [];
    } else {
      NodeEdges = settings.EdgeGroups
          .SelectMany(x => x.AddedEdges)
          .Select(x => new NodeEdge {
              Start = _nodeIdService.GridToId(x.Start),
              End = _nodeIdService.GridToId(x.End),
          })
          .ToList();
    }
  }

  /// <summary>Updates the properties that depend on the district's NavMesh.</summary>
  /// <remarks>
  /// The site must be within the range from the road, which is currently 9 tiles. We intentionally ignore the accesses
  /// below the site level: even though the game can construct sites this way (one tile above the road), the path
  /// checking algo cannot deal with all the corners cases. It may limit players in their construction methods, but
  /// that's the price to pay.
  /// </remarks>
  public void UpdateNavMesh() {
    NavMeshUpdateTimer.Start();
    PathCheckingService.PathUpdateProfiler.StartNewHit();
    if (RestrictedNodes == null) {
      InitializeNavMesh();
    }
    ResetState();

    var bestDistance = float.MaxValue;
    RoadSpillFlowField bestFlow = null;
    var minAccessHeight = CoordinateSystem.GridToWorld(BlockObject.Coordinates).y;
    var accessNodes = _accessible.Accesses
        .Where(access => access.y >= minAccessHeight && _nodeIdService.Contains(access))
        .Select(_nodeIdService.WorldToId);
    foreach (var accessNode in accessNodes) {
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
        BestAccessNode = accessNode;
        bestFlow = flow;
      }
    }
    if (bestFlow != null) {
      _bestPathRoadNodeId = bestFlow.GetRoadParentNodeId(BestAccessNode);
      BestBuildersPathCornerNodes = GetPathToRoadFast(BestAccessNode, bestFlow);
      BestBuildersPathNodeIndex = BestBuildersPathCornerNodes.ToHashSet();
      ClearAllStates();
    } else {
      if (CanBeAccessedInPreview) {
        SetMaybeReachableStatus();
      } else {
        SetUnreachableStatus();
      }
    }
    PathCheckingService.PathUpdateProfiler.Stop();
    NavMeshUpdateTimer.Stop();
  }

  /// <summary>Fast method of getting a path to the closest road.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static List<int> GetPathToRoadFast(int nodeId, RoadSpillFlowField flow) {
    var res = new List<int>(20) {
        nodeId,
    };
    var roadParentId = flow.GetRoadParentNodeId(nodeId);
    if (nodeId == roadParentId) {
      return res;  // The access is at the road.
    }
    do {
      nodeId = flow.GetParentId(nodeId);
      res.Add(nodeId);
    } while (nodeId != roadParentId);
    return res;
  }

  /// <summary>Blocks the site since there is no controllable ways to reach it.</summary>
  /// <remarks>
  /// The site can actually be reachable for the stock game, but the algo may not be aware of it. So, just block the
  /// site to not get unexpected blocks in the other chains.
  /// </remarks>
  void SetUnreachableStatus() {
    _unreachableStatusToggle.Activate();
    _maybeReachableStatusToggle.Deactivate();
    _blockableObject.Block(this);
  }

  /// <summary>Blocks site, but indicates that there can be path being built.</summary>
  void SetMaybeReachableStatus() {
    _unreachableStatusToggle.Deactivate();
    _maybeReachableStatusToggle.Activate();
    _blockableObject.Block(this);
  }

  /// <summary>A cleanup method that resets all effects on the site and resumes the stock one.</summary>
  void ClearAllStates() {
    _unreachableStatusToggle.Deactivate();
    _maybeReachableStatusToggle.Deactivate();
    _blockableObject.Unblock(this);
    if (_isCurrentlySelected && _entityReachabilityStatus) {
      _entityReachabilityStatus.OnSelect();
    }
  }

  /// <summary>Reset to unreachable site.</summary>
  void ResetState() {
    BestBuildersPathNodeIndex = [];
    BestBuildersPathCornerNodes = [];
    BestAccessNode = -1;
    _bestPathRoadNodeId = -1;
    CanBeAccessedInPreview = false;
  }

  #endregion

  #region Events for the state update

  /// <summary>Reacts on construction complete and verifies if a non-grounded site became grounded.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnBlockObjectEnteredFinishedStateEvent(EnteredFinishedStateEvent e) {
    if (!_groundedSite.IsValid) {
      return;
    }
    if (!_isValid) {
      _isValid = true;
      UpdateNavMesh();
    }
  }

  /// <summary>Deleted entities can unblock unreachable sites.</summary>
  /// <remarks>Needs to be public to work.</remarks>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent @event) {
    if (!_isValid) {
      return;
    }
    if (BestAccessNode == -1 && @event.Entity.GetComponent<BlockObject>() != BlockObject) {
      UpdateNavMesh();
    }
  }

  #endregion

  #region IAwakableComponent implementation

  /// <summary>It is called before <see cref="InjectDependencies"/>!</summary>
  public void Awake() {
    BlockObject = AutomationBehavior.GetComponent<BlockObject>();
    if (BlockObject.IsPreview) {
      throw new InvalidOperationException($"{DebugEx.BaseComponentToString(BlockObject)} must not be in preview");
    }
    ConstructionSite = AutomationBehavior.GetComponent<ConstructionSite>();
    _blockableObject = AutomationBehavior.GetComponent<BlockableObject>();
    _entityReachabilityStatus = AutomationBehavior.GetComponent<EntityReachabilityStatus>();
    _groundedSite = AutomationBehavior.GetComponent<GroundedConstructionSite>();
    _accessible = AutomationBehavior.GetComponent<ConstructionSiteAccessible>().Accessible;
    _blockObjectNavMesh = AutomationBehavior.GetComponent<BlockObjectNavMesh>();
  }

  #endregion

  #region INavMeshListener implemenation

  /// <inheritdoc/>
  public void OnNavMeshUpdated(NavMeshUpdate navMeshUpdate) {
    if (!_isValid) {
      return;
    }
    if (_bestPathRoadNodeId == -1) {
      // For an unreachable site, _any_ update to navmesh can make it reachable.
      // Also, the road doesn't exist on the site were loaded just loaded.
      UpdateNavMesh();
      return;
    }
    // If we have a path, then check if the navmesh change affects at least one of the nodes. No change, no problem.
    if (navMeshUpdate.RoadNodeIds.FastContains(_bestPathRoadNodeId)
        || navMeshUpdate.TerrainNodeIds.FastAny(BestBuildersPathNodeIndex.Contains)) {
      UpdateNavMesh();
    }
  }

  #endregion

  #region ISelectionListener implemenation

  /// <inheritdoc/>
  public void OnSelect() {
    _isCurrentlySelected = true;
    if (!_entityReachabilityStatus) {
      return;
    }
    if (_unreachableStatusToggle.IsActive || _maybeReachableStatusToggle.IsActive) {
      _entityReachabilityStatus.OnUnselect();  // We show our own status.
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
    DisableSiteComponent();
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    DisableSiteComponent();
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
    var statusObject = AutomationBehavior.GetComponent<StatusSubject>();
    statusObject.RegisterStatus(_unreachableStatusToggle);
    statusObject.RegisterStatus(_maybeReachableStatusToggle);
    _isValid = _groundedSite.IsValid;
    EnableSiteComponent();
  }

  #endregion
}