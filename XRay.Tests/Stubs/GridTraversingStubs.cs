using System.Collections.Generic;
using UnityEngine;

namespace Timberborn.GridTraversing;

class GridTraversal {
  public readonly List<RayHit> Hits = [];

  public IEnumerable<RayHit> TraverseRay(Ray ray) {
    return Hits;
  }

  public readonly record struct RayHit(Vector3Int Coordinates, Vector3Int Face, Vector3 Intersection) {
    public static RayHit Top(Vector3Int coordinates) {
      return new RayHit(coordinates, new Vector3Int(0, 0, 1), new Vector3(coordinates.x, coordinates.y, coordinates.z));
    }
  }
}
