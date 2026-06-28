// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Rendering;
using Timberborn.SelectionSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterBuildings;

/// <summary>This component shows an output level indicator.</summary>
/// <remarks>
/// It will be added to any building with <see cref="AdjustableWaterOutput"/> component with the default settings.
/// However, if it's already in the building blueprint, it will retain the settings.
/// </remarks>
sealed class AdjustableWaterOutputMarker(MarkerDrawerFactory markerDrawerFactory)
    : BaseComponent, IAwakableComponent, IUpdatableComponent, ISelectionListener {

  static readonly Color AboveLevelMarkerColor = Color.green;
  static readonly Color BelowLevelMarkerColor = Color.blue;
  static readonly float MarkerYOffset = 0.02f;

  #region ISelectionListener implemetation

  /// <inheritdoc/>
  public void OnSelect() {
    _selected = true;
    RefreshVisibility();
  }

  /// <inheritdoc/>
  public void OnUnselect() {
    _selected = false;
    DisableComponent();
  }

  #endregion

  #region Implementation of IAwakableComponent

  /// <inheritdoc/>
  public void Awake() {
    _adjustableWaterOutput = GetComponent<AdjustableWaterOutput>();
    _blockObject = GetComponent<BlockObject>();
    _markerDrawer = markerDrawerFactory.CreateTileDrawer();
    DisableComponent();
  }

  #endregion

  #region Implementation of IUpdatableComponent

  /// <inheritdoc/>
  public void Update() {
    RefreshVisibility();
    if (!Enabled) {
      return;
    }
    var targetCoordinates = _adjustableWaterOutput.TargetCoordinates;
    var coordinates = new Vector3Int(targetCoordinates.x, targetCoordinates.y, _adjustableWaterOutput.MaxHeight); 
    var currentWaterLevel = _adjustableWaterOutput.CurrentWaterLevel;
    var currentLevelLimit = _adjustableWaterOutput.TargetWaterLevel;
    var markerOffset = currentLevelLimit - _adjustableWaterOutput.MaxHeight;
    var color  = currentWaterLevel < currentLevelLimit ? AboveLevelMarkerColor : BelowLevelMarkerColor;
    _markerDrawer.DrawAtCoordinates(coordinates, markerOffset + MarkerYOffset, color);
  }

  #endregion

  #region Implementation

  internal void RefreshVisibility() {
    if (_selected && !_blockObject.IsPreview && _adjustableWaterOutput.ShowHeightMarker) {
      EnableComponent();
    } else {
      DisableComponent();
    }
  }

  AdjustableWaterOutput _adjustableWaterOutput;
  MeshDrawer _markerDrawer;
  BlockObject _blockObject;
  bool _selected;

  #endregion
}
