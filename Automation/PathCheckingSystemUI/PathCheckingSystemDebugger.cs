// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.PathCheckingSystem;
using Timberborn.AssetSystem;
using Timberborn.Coordinates;
using Timberborn.Debugging;
using Timberborn.Navigation;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.WalkingSystemUI;
using UnityEngine;

namespace IgorZ.Automation.PathCheckingSystemUI;

/// <summary>Shows corner markers for the path from the road to the accessible of the selected site.</summary>
sealed class PathCheckingSystemDebugger : ILoadableSingleton, IUpdatableSingleton {

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {
    _eventBus.Register(this);
    _blockerMarker = Object.Instantiate(_walkerDebugger._destinationMarker);
  }

  #endregion

  #region IUpdatableSingleton

  /// <inheritdoc/>
  public void UpdateSingleton() {
    if (_debugModeManager.Enabled && _selectedSite != null) {
      if (_pathCornersIndex == null || !_pathCornersIndex.SetEquals(_selectedSite.BestBuildersPathNodeIndex)) {
        _pathCornersIndex = _selectedSite.BestBuildersPathNodeIndex;
        ResetMarkers();
      }
    } else {
      HideMarkers();
    }
  }
  HashSet<int> _pathCornersIndex;

  #endregion

  #region Implementation

  readonly EventBus _eventBus;
  readonly NodeIdService _nodeIdService;
  readonly DebugModeManager _debugModeManager;
  readonly List<GameObject> _cornerMarkers = [];
  readonly GameObject _cornerMarkerPrefab;
  readonly WalkerDebugger _walkerDebugger;

  GameObject _blockerMarker;
  PathCheckingSite _selectedSite;

  PathCheckingSystemDebugger(EventBus eventBus, NodeIdService nodeIdService, WalkerDebugger walkerDebugger,
                             DebugModeManager debugModeManager, IAssetLoader assetLoader) {
    _eventBus = eventBus;
    _nodeIdService = nodeIdService;
    _debugModeManager = debugModeManager;
    _cornerMarkerPrefab = walkerDebugger._cornerMarkerPrefab;
    _walkerDebugger = walkerDebugger;
  }

  void HideMarkers() {
    for (var index = _cornerMarkers.Count - 1; index >= 0; index--) {
      var cornerMarker = _cornerMarkers[index];
      Object.Destroy(cornerMarker);
    }
    _blockerMarker.SetActive(false);
    _cornerMarkers.Clear();
  }

  void ResetMarkers() {
    HideMarkers();
    var pathCorners = _selectedSite.BestBuildersPathCornerNodes.Select(x => _nodeIdService.IdToWorld(x));
    foreach (var pathCorner in pathCorners) {
      _cornerMarkers.Add(Object.Instantiate(_cornerMarkerPrefab, pathCorner, Quaternion.identity));
    }

    if (_selectedSite.BlockedSite != null) {
      _blockerMarker.SetActive(true);
      _blockerMarker.transform.position =
          CoordinateSystem.GridToWorldCentered(_selectedSite.BlockedSite.BlockObject.Coordinates);
    } else {
      _blockerMarker.SetActive(false);
    }
  }

  #endregion

  #region Events

  [OnEvent]
  public void OnSelectableObjectSelected(SelectableObjectSelectedEvent @event) {
    var automationBehavior = @event.SelectableObject.GetComponent<AutomationBehavior>();
    if (!automationBehavior || !automationBehavior.TryGetDynamicComponent<PathCheckingSite>(out var site)) {
      return;
    }
    if (site.Enabled && site.BestAccessNode != -1) {
      _selectedSite = site;
    }
  }

  [OnEvent]
  public void OnSelectableObjectUnselected(SelectableObjectUnselectedEvent @event) {
    _selectedSite = null;
    _pathCornersIndex = null;
  }

  #endregion
}