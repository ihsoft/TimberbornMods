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
using Timberborn.Buildings;
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

  /// <summary>Path edges of this site if it's a path building. It's <c>null</c> for non-path buildings.</summary>
  public readonly List<Edge> Edges;

  /// <summary>The path from the closest road. If it's empty, then the site cannot be reached.</summary>
  /// <seealso cref="CanBeAccessedInPreview"/>
  public HashSet<Vector3Int> BestBuildersPath {
    get {
      if (_bestBuildersPath == null) {
        UpdateBestPath();
      }
      return _bestBuildersPath;
    }
  }

  /// <summary>Indicates that the <see cref="BestBuildersPath"/> is dirty and will be updated if accessed.</summary>
  /// <remarks>
  /// The other properties that depend on the path (e.g. <see cref="CanBeAccessedInPreview"/>) should be treated as
  /// invalid when this state is ON.
  /// </remarks>
  public bool NeedsBestPathUpdate => _bestBuildersPath == null;

  /// <summary>Indicates that the site _may_ become reachable when all the preview buildings are built.</summary>
  /// <remarks>
  /// It's a best effort check. There is no guarantee the preview buildings are actually providing access.
  /// </remarks>
  public bool CanBeAccessedInPreview;

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

  int _bestPathRoadNodeId = -1;
  HashSet<int> _bestPathNodeIds;
  HashSet<Vector3Int> _bestBuildersPath;
  bool _isFullyGrounded;

  /// <exception cref="InvalidOperationException"> if the site doesn't have all teh expected components.</exception>
  PathCheckingSite(BlockObject blockObject) {
    Coordinates = blockObject.Coordinates;
    ConstructionSite = blockObject.GetComponentFast<ConstructionSite>();
    _groundedSite = blockObject.GetComponentFast<GroundedConstructionSite>();
    _isFullyGrounded = _groundedSite.IsFullyGrounded;
    _accessible = blockObject.GetComponentFast<ConstructionSiteAccessible>().Accessible;
    _unreachableStatus = blockObject.GetComponentFast<UnreachableStatus>();
    if (!_unreachableStatus) {
      _unreachableStatus = _baseInstantiator.AddComponent<UnreachableStatus>(blockObject.GameObjectFast);
      _unreachableStatus.Initialize(_loc);
    }
    if (!ConstructionSite || !_groundedSite || !_accessible) {
      throw new InvalidOperationException(
          DebugEx.BaseComponentToString(blockObject) + " is not a valid construction site");
    }

    var building = blockObject.GetComponentFast<Building>();
    if (building && building.Path) {
      var settings = blockObject.GetComponentFast<BlockObjectNavMeshSettings>();
      if (settings && !settings.BlockAllEdges) {
        var manuallySetEdges = settings.ManuallySetEdges().ToList();
        if (manuallySetEdges.Count > 0) {
          Edges = manuallySetEdges;
        }
      }
    }
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

  /// <summary>Forces the <see cref="BestBuildersPath"/> update on the next access to it.</summary>
  void RequestBestPathUpdate() {
    _bestBuildersPath = null;
  }

  /// <summary>Updates the <see cref="BestBuildersPath"/> to the site from the closets road node.</summary>
  /// <remarks>
  /// The site must be within the range from the road, which is currently 9 tiles. We intentionally ignore the accesses
  /// below the site level: even though the game can construct sites this way (one tile above the road), the path
  /// checking algo cannot deal with all the corners cases. It may limit players in their construction methods, but
  /// that's the price to pay.
  /// </remarks>
  void UpdateBestPath() {
    _bestBuildersPath = new HashSet<Vector3Int>();
    _bestPathRoadNodeId = -1;
    _bestPathNodeIds = new HashSet<int>();
    CanBeAccessedInPreview = false;
    if (!_isFullyGrounded) {
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
      _bestPathNodeIds = pathCorners.Select(_nodeIdService.WorldToId).ToHashSet();
      _bestBuildersPath = pathCorners.Select(NavigationCoordinateSystem.WorldToGridInt).ToHashSet();
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
    if (!_isFullyGrounded) {
      return;  // The ungrounded site just cannot start building.
    }
    if (_bestPathRoadNodeId == -1) {
      RequestBestPathUpdate();  // For an unreachable site, _any_ update to navmesh can make it reachable.
      return;
    }
    // Otherwise, if the navmesh change affects at least one of the road nodes of teh site, then it's a trigger.
    if (navMeshUpdate.RoadNodeIds.FastContains(_bestPathRoadNodeId)
        || navMeshUpdate.TerrainNodeIds.FastAny(_bestPathNodeIds.Contains)) {
      RequestBestPathUpdate();
    }
  }

  /// <summary>Reacts on construction complete and verifies if a non-grounded site can now start building.</summary>
  internal void OnConstructibleCompleted(Constructible constructible) {
    if (_isFullyGrounded || !_groundedSite.IsFullyGrounded) {
      return;
    }
    _isFullyGrounded = true;
    RequestBestPathUpdate();
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
