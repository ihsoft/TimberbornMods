using System.Collections.Generic;

namespace Timberborn.Common;

public static class CollectionExtensions {
  public static bool IsEmpty<T>(this ICollection<T> collection) {
    return collection.Count == 0;
  }
}
