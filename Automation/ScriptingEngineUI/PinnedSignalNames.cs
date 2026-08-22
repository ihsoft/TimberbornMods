// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class PinnedSignalNames {
  readonly Dictionary<string, string> _names = [];

  public void Clear() {
    _names.Clear();
  }

  public string GetDisplayName(string signalName, string defaultName) {
    return _names.GetValueOrDefault(signalName) ?? defaultName;
  }

  public string GetSavedName(string signalName) {
    return _names.GetValueOrDefault(signalName) ?? "";
  }

  public void Set(string signalName, string pinnedName) {
    pinnedName = pinnedName.Trim();
    if (string.IsNullOrEmpty(pinnedName)) {
      _names.Remove(signalName);
    } else {
      _names[signalName] = pinnedName;
    }
  }

  public void Remove(string signalName) {
    _names.Remove(signalName);
  }
}
