using IgorZ.XRay.Core;
using Timberborn.BlockObjectPickingSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.Coordinates;
using Timberborn.GridTraversing;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace XRay.Tests;

static class TerrainRayCasterTests {
  public static void ReturnsSurfaceTerrainHits() {
    var terrain = new FakeTerrainService();
    var traversal = new GridTraversal();
    var previewPicker = new BlockObjectPreviewPicker();
    var rayCaster = CreateRayCaster(terrain, traversal, previewPicker);
    var coordinates = new Vector3Int(2, 3, 4);
    var spec = CreateSpec(underground: false);
    terrain.AddColumn(coordinates.XY());
    previewPicker.TerrainOrStackable.Add(coordinates);
    traversal.Hits.Add(GridTraversal.RayHit.Top(coordinates));

    var result = rayCaster.CenteredPreviewCoordinates(spec, Orientation.Cw0, new Ray());

    Assert.True(result.HasValue);
    Assert.Equal(new PickedCoordinates(coordinates, coordinates.z, 0, CanAttachToSide: false), result.Value);
  }

  public static void StopsOnBlockingObject() {
    var terrain = new FakeTerrainService();
    var traversal = new GridTraversal();
    var previewPicker = new BlockObjectPreviewPicker();
    var rayCaster = CreateRayCaster(terrain, traversal, previewPicker);
    var coordinates = new Vector3Int(2, 3, 4);
    var spec = CreateSpec(underground: false);
    previewPicker.BlockingObjectCoordinates.Add(coordinates);
    traversal.Hits.Add(GridTraversal.RayHit.Top(coordinates));

    var result = rayCaster.CenteredPreviewCoordinates(spec, Orientation.Cw0, new Ray());

    Assert.False(result.HasValue);
  }

  public static void SupportsSideAttachedPreviews() {
    var terrain = new FakeTerrainService();
    var traversal = new GridTraversal();
    var previewPicker = new BlockObjectPreviewPicker();
    var rayCaster = CreateRayCaster(terrain, traversal, previewPicker);
    var coordinates = new Vector3Int(2, 3, 4);
    var face = new Vector3Int(1, 0, 0);
    var intersection = new Vector3(2.5f, 3.5f, 4.25f);
    var spec = CreateSpec(underground: false, canAttachToSide: true);
    terrain.AddColumn(coordinates.XY());
    previewPicker.TerrainOrUnfinishedTerrain.Add(coordinates);
    previewPicker.StackableBelow.Add(coordinates + face);
    traversal.Hits.Add(new GridTraversal.RayHit(coordinates, face, intersection));

    var result = rayCaster.CenteredPreviewCoordinates(spec, Orientation.Cw0, new Ray());

    Assert.True(result.HasValue);
    Assert.Equal(new PickedCoordinates(coordinates + face, intersection.z, 0, CanAttachToSide: true), result.Value);
  }

  public static void PrefersUndergroundHitsAfterSurfaceFallback() {
    var terrain = new FakeTerrainService();
    var traversal = new GridTraversal();
    var previewPicker = new BlockObjectPreviewPicker();
    var rayCaster = CreateRayCaster(terrain, traversal, previewPicker);
    var firstHit = new Vector3Int(2, 3, 5);
    var secondHit = new Vector3Int(2, 3, 3);
    var spec = CreateSpec(underground: true);
    terrain.AddUnderground(firstHit);
    terrain.AddTerrain(secondHit.Above());
    terrain.AddUnderground(secondHit);
    previewPicker.TerrainWithStump.Add(firstHit);
    previewPicker.TerrainWithStump.Add(secondHit);
    traversal.Hits.Add(GridTraversal.RayHit.Top(firstHit));
    traversal.Hits.Add(GridTraversal.RayHit.Top(secondHit));

    var result = rayCaster.CenteredPreviewCoordinates(spec, Orientation.Cw0, new Ray());

    Assert.True(result.HasValue);
    Assert.Equal(new PickedCoordinates(secondHit.Below(), secondHit.z, -1, CanAttachToSide: false), result.Value);
  }

  static TerrainRayCaster CreateRayCaster(
      ITerrainService terrainService, GridTraversal gridTraversal, BlockObjectPreviewPicker previewPicker) {
    return TestObjectFactory.Create<TerrainRayCaster>(
        ("_terrainService", terrainService),
        ("_gridTraversal", gridTraversal),
        ("_blockObjectPreviewPicker", previewPicker));
  }

  static PlaceableBlockObjectSpec CreateSpec(bool underground, bool canAttachToSide = false) {
    return new PlaceableBlockObjectSpec {
        CanBeAttachedToTerrainSide = canAttachToSide,
        BlockObjectSpec = new BlockObjectSpec {
            Blocks = [
                new BlockSpec { Underground = underground },
            ],
        },
    };
  }
}
