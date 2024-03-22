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
using Timberborn.BuilderHubSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.BuildingsNavigation;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.ConstructionSites;
using Timberborn.Coordinates;
using Timberborn.GameDistricts;
using Timberborn.Localization;
using Timberborn.Navigation;
using Timberborn.SelectionSystem;
using Timberborn.StatusSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace Automation.Conditions {

/// <summary>Container for the path blocking site.</summary>
sealed class PathCheckingSite {
  #region API
  // ReSharper disable MemberCanBePrivate.Global

  /// <summary>All path sites index by the grid coordinates.</summary>
  public static readonly Dictionary<Vector3Int, PathCheckingSite> SitesByCoordinates = new();

  /// <summary>Site's coordinates.</summary>
  public readonly Vector3Int Coordinates;

  /// <summary>Construction site cached instance.</summary>
  // FIXME: It's temporary! Use site blockers instead.
  public readonly ConstructionSite ConstructionSite;

  public bool IsFullyGrounded => _groundedSite.IsFullyGrounded;

  /// <summary>Path edges from the site if there are any or <c>null</c>.</summary>
  public List<NavMeshEdge> Edges { get; private set; }

  /// <summary>The path from the closest road to the closets construction site's accessible.</summary>
  /// <seealso cref="MaybeUpdateNavMesh"/>
  public List<Vector3Int> BestBuildersPathCorners { get; private set; }

  /// <summary>The best path index. If it's empty, then the site cannot be reached.</summary>
  /// <seealso cref="CanBeAccessedInPreview"/>
  /// <seealso cref="MaybeUpdateNavMesh"/>
  /// <seealso cref="BestBuildersPathCorners"/>
  public HashSet<Vector3Int> BestBuildersPath { get; private set; }

  /// <summary>Coordinates of the positions that are taken by the site.</summary>
  public List<Vector3Int> RestrictedCoordinates { get; private set; }

  /// <summary>Indicates that all the fields related to path and NavMesh are invalid and need to be updated.</summary>
  /// <seealso cref="MaybeUpdateNavMesh"/>
  public bool NeedsBestPathUpdate => BestBuildersPath == null;

  /// <summary>Indicates that the site _may_ become reachable when all the preview buildings are built.</summary>
  /// <remarks>
  /// It's a best effort check. There is no guarantee the preview buildings are actually providing access. The
  /// <see cref="BestBuildersPathCorners"/> can be absent for such sites.
  /// </remarks>
  public bool CanBeAccessedInPreview { get; private set; }

  /// <summary>Finds the existing construction site or creates a new one.</summary>
  /// <seealso cref="SitesByCoordinates"/>
  public static PathCheckingSite GetOrCreate(BlockObject blockObject) {
    if (!SitesByCoordinates.TryGetValue(blockObject.Coordinates, out var cachedSite)) {
      var site = new PathCheckingSite(blockObject);
      SitesByCoordinates.Add(site.Coordinates, site);
      cachedSite = site;
    }
    return cachedSite;
  }

  /// <summary>Drops the site and all internal caches associated with it.</summary>
  public void Destroy() {
    SitesByCoordinates.Remove(Coordinates);
    if (_unreachableStatus) {
      _unreachableStatus.Cleanup();
    }
  }

  /// <summary>Verifies that the all NavMesh related things are up to date on the site.</summary>
  /// <remarks>If no updates needed, it is a very cheap call.</remarks>
  /// <seealso cref="NeedsBestPathUpdate"/>
  /// <seealso cref="BestBuildersPath"/>
  /// <seealso cref="BestBuildersPathCorners"/>
  public void MaybeUpdateNavMesh() {
    if (NeedsBestPathUpdate) {
      UpdateBestPath();
    }
  }

  // ReSharper restore MemberCanBePrivate.Global
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

  static BaseInstantiator _baseInstantiator;
  static ILoc _loc;
  static NodeIdService _nodeIdService;
  static DistrictCenterRegistry _districtCenterRegistry;
  static DistrictMap _districtMap;
  static PreviewDistrictMap _previewDistrictMap;
  static INavigationService _navigationService;

  readonly Accessible _accessible;
  readonly UnreachableStatus _unreachableStatus;
  readonly GroundedConstructionSite _groundedSite;
  readonly BlockObjectNavMesh _blockObjectNavMesh;

  int _bestPathRoadNodeId = -1;
  HashSet<int> _bestPathNodeIds;

  /// <exception cref="InvalidOperationException"> if the site doesn't have all teh expected components.</exception>
  PathCheckingSite(BlockObject blockObject) {
    Coordinates = blockObject.Coordinates;
    ConstructionSite = blockObject.GetComponentFast<ConstructionSite>();
    _groundedSite = blockObject.GetComponentFast<GroundedConstructionSite>();
    _accessible = blockObject.GetComponentFast<ConstructionSiteAccessible>().Accessible;
    _unreachableStatus = blockObject.GetComponentFast<UnreachableStatus>();
    _blockObjectNavMesh = blockObject.GetComponentFast<BlockObjectNavMesh>();
    if (!_unreachableStatus) {
      _unreachableStatus = _baseInstantiator.AddComponent<UnreachableStatus>(blockObject.GameObjectFast);
      _unreachableStatus.Initialize(_loc);
    }
    if (!ConstructionSite || !_groundedSite || !_accessible) {
      throw new InvalidOperationException(
          $"{DebugEx.BaseComponentToString(blockObject)} is not a valid construction site");
    }
  }

  /// <summary>Initializes the NavMesh related things.</summary>
  /// <remarks>This must be done on an object hat is already added to the game's NavMesh.</remarks>
  void InitializeNavMesh() {
    var navMeshObject = _blockObjectNavMesh.NavMeshObject;
    RestrictedCoordinates = navMeshObject._restrictedCoordinates;
    Edges = navMeshObject._addingChanges
        .Where(x => x.NavMeshChangeType == NavMeshChangeType.AddEdge)
        .Select(x => x.NavMeshEdge)
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

  void RequestBestPathUpdate() {
  /// <summary>
  /// Resets <see cref="BestBuildersPath"/> and <see cref="BestBuildersPathCorners"/> to trigger the path rebuild.
  /// </summary>
    BestBuildersPath = null;
    BestBuildersPathCorners = null;
  }

  /// <summary>Updates the <see cref="BestBuildersPath"/> to the site from the closets road node.</summary>
  /// <remarks>
  /// The site must be within the range from the road, which is currently 9 tiles. We intentionally ignore the accesses
  /// below the site level: even though the game can construct sites this way (one tile above the road), the path
  /// checking algo cannot deal with all the corners cases. It may limit players in their construction methods, but
  /// that's the price to pay.
  /// </remarks>
  void UpdateBestPath() {
    if (RestrictedCoordinates == null) {
      InitializeNavMesh();
    }
    BestBuildersPath = new HashSet<Vector3Int>();
    BestBuildersPathCorners = new List<Vector3Int>();
    _bestPathRoadNodeId = -1;
    _bestPathNodeIds = new HashSet<int>();
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
    var worldCoords = CoordinateSystem.GridToWorld(Coordinates);
    var accesses = _accessible.Accesses
        .Where(access => access.y >= worldCoords.y && _nodeIdService.Contains(access));
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
      BestBuildersPathCorners = pathCorners.Select(NavigationCoordinateSystem.WorldToGridInt).ToList();
      _bestPathNodeIds = pathCorners.Select(_nodeIdService.WorldToId).ToHashSet();
      BestBuildersPath = pathCorners.Select(NavigationCoordinateSystem.WorldToGridInt).ToHashSet();
      _bestPathRoadNodeId = bestRoadNode;
      _unreachableStatus.Cleanup();
    } else {
      if (CanBeAccessedInPreview) {
        _unreachableStatus.SetMaybeReachable();
      } else {
        _unreachableStatus.SetUnreachable();
      }
    }
  }

  #endregion

  #region Callbacks for the state update

  /// <summary>It's expected to be called from <see cref="PathCheckingController"/> when the navmesh changes.</summary>
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
        || navMeshUpdate.TerrainNodeIds.FastAny(_bestPathNodeIds.Contains)) {
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

  #region BaseComponent for custom unreachable status

  internal sealed class UnreachableStatus : BaseComponent, ISelectionListener {
    const string NotYetReachableLocKey = "IgorZ.Automation.CheckAccessBlockCondition.NotYetReachable";
    const string UnreachableStatusLocKey = "IgorZ.Automation.CheckAccessBlockCondition.UnreachableStatus";
    const string UnreachableAlertLocKey = "IgorZ.Automation.CheckAccessBlockCondition.UnreachableAlert";
    const string UnreachableIconName = "UnreachableObject";

    StatusToggle _unreachableStatusToggle;
    StatusToggle _maybeReachableStatusToggle;
    BlockableBuilding _blockableBuilding;
    BuilderJobReachabilityStatus _builderJobReachabilityStatus;
    bool _isSelected;

    void Awake() {
      _blockableBuilding = GetComponentFast<BlockableBuilding>();
      _builderJobReachabilityStatus = GetComponentFast<BuilderJobReachabilityStatus>();
    }

    /// <summary>Initializes the component since the normal Bindito logic doesn't work here.</summary>
    public void Initialize(ILoc loc) {
      _unreachableStatusToggle = StatusToggle.CreatePriorityStatusWithAlertAndFloatingIcon(
          UnreachableIconName, loc.T(UnreachableStatusLocKey), loc.T(UnreachableAlertLocKey));
      GetComponentFast<StatusSubject>().RegisterStatus(_unreachableStatusToggle);
      _maybeReachableStatusToggle = StatusToggle.CreateNormalStatus(UnreachableIconName, loc.T(NotYetReachableLocKey));
      GetComponentFast<StatusSubject>().RegisterStatus(_maybeReachableStatusToggle);
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
      _maybeReachableStatusToggle.Deactivate();
      _blockableBuilding.Block(this);
    }

    public void SetMaybeReachable() {
      _unreachableStatusToggle.Deactivate();
      _maybeReachableStatusToggle.Activate();
      _blockableBuilding.Block(this);
    }

    /// <summary>A cleanup method that resets all effect on the site.</summary>
    public void Cleanup() {
      _unreachableStatusToggle.Deactivate();
      _maybeReachableStatusToggle.Deactivate();
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
      if (_unreachableStatusToggle.IsActive || _maybeReachableStatusToggle.IsActive) {
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
}

}
