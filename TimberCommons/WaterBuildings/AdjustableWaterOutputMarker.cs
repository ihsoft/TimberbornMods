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
/// However, if it's already on the prefab, it wil retain the settings. 
/// </remarks>
sealed class AdjustableWaterOutputMarker(MarkerDrawerFactory markerDrawerFactory)
    : BaseComponent, IAwakableComponent, IUpdatableComponent, ISelectionListener {

  static readonly Color AboveLevelMarkerColor = Color.green;
  static readonly Color BelowLevelMarkerColor = Color.blue;
  static readonly float MarkerYOffset = 0.02f;

  #region ISelectionListener implemetation

  /// <inheritdoc/>
  public void OnSelect() {
    if (!_blockObject.IsPreview && _adjustableWaterOutput.ShowHeightMarker) {
      EnableComponent();
    }
  }

  /// <inheritdoc/>
  public void OnUnselect() {
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
    var targetCoordinates = _adjustableWaterOutput.TargetCoordinates;
    var coordinates = new Vector3Int(targetCoordinates.x, targetCoordinates.y, _adjustableWaterOutput.MaxHeight); 
    var currentWaterLevel = _adjustableWaterOutput.CurrentWaterLevel;
    var currentLevelLimit = _adjustableWaterOutput.MaxHeight + _adjustableWaterOutput.SpillwayHeightDelta;
    var color  = currentWaterLevel < currentLevelLimit ? AboveLevelMarkerColor : BelowLevelMarkerColor;
    _markerDrawer.DrawAtCoordinates(coordinates, _adjustableWaterOutput.SpillwayHeightDelta + MarkerYOffset, color);
  }

  #endregion

  #region Implementation

  AdjustableWaterOutput _adjustableWaterOutput;
  MeshDrawer _markerDrawer;
  BlockObject _blockObject;

  #endregion
}
