// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Rendering;
using Timberborn.SelectionSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterBuildings;

/// <summary>This component shows an output level indicator.</summary>
/// <remarks>
/// It will be added to any building with <see cref="AdjustableWaterOutput"/> component with teh default settings.
/// However, if it's already on the prefab, it wil retain the settings. 
/// </remarks>
sealed class AdjustableWaterOutputMarker : BaseComponent, ISelectionListener {

  #region Fields for Unity
  // ReSharper disable InconsistentNaming

  [SerializeField]
  [Tooltip("The color of the 'hovering' p[lane that shows the water spillway depth limit.")]
  Color _markerColor = Color.blue;

  [SerializeField]
  [Tooltip("Defines for how long the marker plane should go off the building. A purely cosmetic purpose.")]
  float _markerYOffset = 0.02f;

  // ReSharper restore InconsistentNaming
  #endregion

  #region ISelectionListener implemetation

  /// <inheritdoc/>
  public void OnSelect() {
    enabled = !_blockObject.IsPreview && _adjustableWaterOutput.ShowHeightMarker;
  }

  /// <inheritdoc/>
  public void OnUnselect() {
    enabled = false;
  }

  #endregion

  #region Implementation

  MarkerDrawerFactory _markerDrawerFactory;
  AdjustableWaterOutput _adjustableWaterOutput;
  MeshDrawer _markerDrawer;
  BlockObject _blockObject;

  [Inject]
  public void InjectDependencies(MarkerDrawerFactory markerDrawerFactory) {
    _markerDrawerFactory = markerDrawerFactory;
  }

  void Awake() {
    _adjustableWaterOutput = GetComponentFast<AdjustableWaterOutput>();
    _blockObject = GetComponentFast<BlockObject>();
    _markerDrawer = _markerDrawerFactory.CreateTileDrawer(_markerColor);
    enabled = false;
  }

  void Update() {
    var targetCoordinates = _adjustableWaterOutput.TargetCoordinates;
    var coordinates = new Vector3Int(targetCoordinates.x, targetCoordinates.y, _adjustableWaterOutput.MaxHeight); 
    _markerDrawer.DrawAtCoordinates(coordinates, _adjustableWaterOutput.SpillwayHeightDelta + _markerYOffset);
  }

  #endregion
}
