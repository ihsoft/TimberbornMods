using System.Collections.Generic;
using UnityEngine;

namespace Timberborn.Common;

public static class CollectionExtensions {
  public static bool IsEmpty<T>(this ICollection<T> collection) {
    return collection.Count == 0;
  }
}

public static class VectorExtensions {
  public static Vector2Int XY(this Vector3Int coordinates) {
    return new Vector2Int(coordinates.x, coordinates.y);
  }
}
