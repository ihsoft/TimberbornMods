// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;

namespace IgorZ.Automation.ScriptingEngine.Core;

/// <summary>Applies and serializes the player-defined signal module order.</summary>
static class SignalModuleOrder {

  const char Separator = '|';
  static IReadOnlyDictionary<string, int> _orderByName = new Dictionary<string, int>();

  /// <summary>Loads the serialized module order. An empty value restores the component registration order.</summary>
  public static void Load(string serializedOrder) {
    _orderByName = string.IsNullOrEmpty(serializedOrder)
        ? []
        : serializedOrder
            .Split([Separator], StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .Select((name, index) => (name, index))
            .ToDictionary(item => item.name, item => item.index);
  }

  /// <summary>Serializes module names, preserving their first occurrence.</summary>
  public static string Serialize(IEnumerable<string> moduleNames) {
    return string.Join(Separator.ToString(), moduleNames.Distinct());
  }

  /// <summary>
  /// Orders modules by player preference. Modules absent from the preference keep their registration order at the end.
  /// </summary>
  public static IReadOnlyList<T> Apply<T>(IEnumerable<T> modules, Func<T, string> getModuleName) {
    return modules
        .Select((module, index) => (module, index))
        .OrderBy(item => _orderByName.GetValueOrDefault(getModuleName(item.module), int.MaxValue))
        .ThenBy(item => item.index)
        .Select(item => item.module)
        .ToList();
  }
}
