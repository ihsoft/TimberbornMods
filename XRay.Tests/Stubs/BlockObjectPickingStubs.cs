using System.Collections.Generic;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.GridTraversing;
using UnityEngine;

namespace Timberborn.BlockObjectPickingSystem;

class BlockObjectPreviewPicker {
  public readonly HashSet<Vector3Int> BlockingObjectCoordinates = [];
  public readonly HashSet<Vector3Int> StackableBelow = [];
  public readonly HashSet<Vector3Int> TerrainOrStackable = [];
  public readonly HashSet<Vector3Int> TerrainOrUnfinishedTerrain = [];
  public readonly HashSet<Vector3Int> TerrainWithStump = [];

  public object GetObjectHitByRaycast() {
    return BlockingObjectCoordinates;
  }

  public bool IsTerrainWithStump(Vector3Int coordinates) {
    return TerrainWithStump.Contains(coordinates);
  }

  public bool IsTerrainOrStackable(Vector3Int coordinates) {
    return TerrainOrStackable.Contains(coordinates);
  }

  public bool IsTerrainOrUnfinishedTerrain(Vector3Int coordinates) {
    return TerrainOrUnfinishedTerrain.Contains(coordinates);
  }

  public bool HasStackableBelow(Vector3Int coordinates) {
    return StackableBelow.Contains(coordinates);
  }

  public static Vector3Int ComposeCoordinates(
      Orientation orientation, Vector3Int customPivot, BlockObjectSpec spec, GridTraversal.RayHit item) {
    return item.Coordinates + customPivot;
  }

  public static bool ShouldObjectBlockCoordinates(object objectHitByRaycast, Vector3Int coordinates) {
    return objectHitByRaycast is HashSet<Vector3Int> blockedCoordinates && blockedCoordinates.Contains(coordinates);
  }
}
