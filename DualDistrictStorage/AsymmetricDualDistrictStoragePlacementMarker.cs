using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.Rendering;
using Timberborn.SelectionSystem;
using UnityEngine;

namespace IgorZ.DualDistrictStorage;

sealed class AsymmetricDualDistrictStoragePlacementMarker : BaseComponent,
    IAwakableComponent, IInitializablePreview, ILateUpdatableComponent, IPreviewSelectionListener {
  const float EntranceMarkerYOffset = 0.2f;
  static readonly Vector3Int OppositeEntranceOffset = new(0, 4, 0);

  readonly MarkerDrawerFactory _markerDrawerFactory;

  BlockObject _blockObject;
  MeshDrawer _entranceMarkerMeshDrawer;

  public AsymmetricDualDistrictStoragePlacementMarker(MarkerDrawerFactory markerDrawerFactory) {
    _markerDrawerFactory = markerDrawerFactory;
  }

  public void Awake() {
    _blockObject = GetComponent<BlockObject>();
    DisableComponent();
  }

  public void InitializePreview() {
    _entranceMarkerMeshDrawer = _markerDrawerFactory.CreateEntranceMarkerDrawer();
  }

  public void LateUpdate() {
    var entrance = _blockObject.PositionedEntrance;
    if (entrance == null || _entranceMarkerMeshDrawer == null) {
      return;
    }

    var coordinates = entrance.Coordinates
        + _blockObject.Orientation.Transform(OppositeEntranceOffset);
    var rotation = Quaternion.AngleAxis(
        entrance.Direction2D.Across().ToAngle() + 180f,
        Vector3.up);
    _entranceMarkerMeshDrawer.DrawAtCoordinates(
        coordinates,
        EntranceMarkerYOffset,
        rotation);
  }

  public void OnPreviewSelect() {
    EnableComponent();
  }

  public void OnPreviewUnselect() {
    DisableComponent();
  }
}
