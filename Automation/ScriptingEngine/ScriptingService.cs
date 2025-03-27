// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.SingletonSystem;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Service that provides access to the scripting engine.</summary>
public sealed class ScriptingService : ILoadableSingleton {

  #region API

  /// <summary>Registers a new scriptable component.</summary>
  public void RegisterScriptable(IScriptable scriptable) {
    _registeredScriptables.Add(scriptable.Name, scriptable);
  }

  /// <inheritdoc cref="IScriptable.GetSignalNamesForBuilding"/>
  public string[] GetSignalNamesForBuilding(BaseComponent building) {
    return _registeredScriptables.Values
        .SelectMany(s => s.GetSignalNamesForBuilding(building))
        .ToArray();
  }

  /// <inheritdoc cref="IScriptable.GetSignalSource"/>
  public Func<ScriptValue> GetSignalSource(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetSignalSource(name, building);
  }

  /// <inheritdoc cref="IScriptable.GetSignalDefinition"/>
  public SignalDef GetSignalDefinition(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetSignalDefinition(name, building);
  }

  /// <inheritdoc cref="IScriptable.GetActionNamesForBuilding"/>
  public string[] GetActionNamesForBuilding(BaseComponent building) {
    return _registeredScriptables.Values
        .SelectMany(s => s.GetActionNamesForBuilding(building))
        .ToArray();
  }

  /// <inheritdoc cref="IScriptable.GetActionExecutor"/>
  public Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetActionExecutor(name, building);
  }

  /// <inheritdoc cref="IScriptable.GetActionDefinition"/>
  public ActionDef GetActionDefinition(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetActionDefinition(name, building);
  }

  /// <inheritdoc cref="IScriptable.RegisterSignalChangeCallback"/>
  public void RegisterSignalChangeCallback(string name, AutomationBehavior building, Action onValueChanged) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    scriptable.RegisterSignalChangeCallback(name, building, onValueChanged);
  }

  /// <inheritdoc cref="IScriptable.UnregisterSignalChangeCallback"/>
  public void UnregisterSignalChangeCallback(string name, AutomationBehavior building, Action onValueChanged) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    scriptable.UnregisterSignalChangeCallback(name, building, onValueChanged);
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {}

  #endregion

  #region Implementation

  readonly Dictionary<string, IScriptable> _registeredScriptables = [];

  #endregion
}
