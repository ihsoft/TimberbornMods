// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Timberborn.Common;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils;

static class Extensions {
  /// <summary>Converts readonly hash set to enumerable (for using in LINQ expressions).</summary>
  public static IEnumerable<T> AsEnumerable<T>(this ReadOnlyHashSet<T> enumerator) {
    using var it = enumerator.GetEnumerator();
    while (it.MoveNext()) {
      yield return it.Current;
    }
  }
}
