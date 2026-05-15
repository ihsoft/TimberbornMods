// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlockObjectPickingSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.Coordinates;
using Timberborn.GridTraversing;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace IgorZ.XRay.Core;

sealed class TerrainRayCaster {

  public static TerrainRayCaster Instance { get; private set; }

  readonly ITerrainService _terrainService;
  readonly GridTraversal _gridTraversal;
  readonly BlockObjectPreviewPicker _blockObjectPreviewPicker;

  TerrainRayCaster(
      ITerrainService terrainService, GridTraversal gridTraversal, BlockObjectPreviewPicker blockObjectPreviewPicker) {
    Instance = this;
    _terrainService = terrainService;
    _gridTraversal = gridTraversal;
    _blockObjectPreviewPicker = blockObjectPreviewPicker;
  }

  public PickedCoordinates? CenteredPreviewCoordinates(
      PlaceableBlockObjectSpec placeableBlockObjectSpec, Orientation orientation, Ray ray) {
    var spec = placeableBlockObjectSpec.GetSpec<BlockObjectSpec>();
    var customPivot = placeableBlockObjectSpec.CustomPivot;
    var objectHitByRaycast = _blockObjectPreviewPicker.GetObjectHitByRaycast();
    var canAttachToSide = placeableBlockObjectSpec.CanBeAttachedToTerrainSide;
    var allUnderground = spec.Blocks.FastAll(block => block.Underground);
    PickedCoordinates? surfaceHit = null;
    foreach (var item in _gridTraversal.TraverseRay(ray)) {
      var coordinates = item.Coordinates;
      var isFaceUp = item.Face.z == 1;
      if (_terrainService.Contains(coordinates.XY())) {
        if (isFaceUp) {
          // If surface has already been hit, only consider empty volumes under the surface.
          if (surfaceHit.HasValue
              && _terrainService.Underground(coordinates) && _terrainService.Underground(coordinates.Above())) {
            continue;
          }
          if (allUnderground && _blockObjectPreviewPicker.IsTerrainWithStump(coordinates)
              || !allUnderground && _blockObjectPreviewPicker.IsTerrainOrStackable(coordinates)) {
            var coords = BlockObjectPreviewPicker.ComposeCoordinates(orientation, customPivot, spec, item);
            var terrainHit = allUnderground
                ? new PickedCoordinates(coords.Below(), coords.z, -1, canAttachToSide)
                : new PickedCoordinates(coords, coords.z, 0, canAttachToSide);
            if (!_terrainService.Underground(coordinates)) {
              return terrainHit;  // It's stackable. Don't go any further.
            }
            if (surfaceHit.HasValue) {
              return terrainHit;  // It's an empty volume under the surface.
            }
            surfaceHit = terrainHit;  // Surface detected. Try finding volumes under it.
            continue;
          }
        } else if (allUnderground && _blockObjectPreviewPicker.IsTerrainWithStump(coordinates)
                   || canAttachToSide && _blockObjectPreviewPicker.IsTerrainOrUnfinishedTerrain(coordinates)) {
          // If hit to a side, then don't try to look though the terrain.
          if (!surfaceHit.HasValue) {
            var coords = BlockObjectPreviewPicker.ComposeCoordinates(orientation, customPivot, spec, item);
            if (canAttachToSide) {
              coords += item.Face;
            }
            return new PickedCoordinates(
                coords, item.Intersection.z, 0, _blockObjectPreviewPicker.HasStackableBelow(coords));
          }
        }
      }
      if (BlockObjectPreviewPicker.ShouldObjectBlockCoordinates(objectHitByRaycast, coordinates)) {
        // The voxel is inside an object (e.g. building).
        return null;
      }
    }
    return surfaceHit;
  }
}
