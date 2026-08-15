// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;

namespace IgorZ.Automation.ScriptingEngineUI;

static class ParameterizedItemsGrouper {
  public sealed record Group<T>(string Key, T[] Items);

  public static Group<T>[] MakeGroups<T>(
      IEnumerable<T> items, Func<T, string> groupKeySelector, int minimumGroupSize) {
    return items
        .Select(item => (Item: item, Key: groupKeySelector(item)))
        .Where(x => x.Key != null)
        .GroupBy(x => x.Key, StringComparer.Ordinal)
        .Where(group => group.Count() >= minimumGroupSize)
        .Select(group => new Group<T>(group.Key, group.Select(x => x.Item).ToArray()))
        .ToArray();
  }
}
