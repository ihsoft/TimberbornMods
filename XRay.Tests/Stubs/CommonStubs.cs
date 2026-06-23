using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Timberborn.Common;

static class EnumerableExtensions {
  public static bool FastAll<T>(this IEnumerable<T> source, System.Func<T, bool> predicate) {
    return source.All(predicate);
  }
}

static class Vector3IntExtensions {
  public static Vector3Int Above(this Vector3Int coordinates) {
    return new Vector3Int(coordinates.x, coordinates.y, coordinates.z + 1);
  }

  public static Vector3Int Below(this Vector3Int coordinates) {
    return new Vector3Int(coordinates.x, coordinates.y, coordinates.z - 1);
  }

  public static Vector2Int XY(this Vector3Int coordinates) {
    return new Vector2Int(coordinates.x, coordinates.y);
  }
}
