// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using Timberborn.BaseComponentSystem;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Service that provides access to the scripting engine.</summary>
sealed class ScriptingService {

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
    return ExecuteOnRegisteredComponent(name, scriptable => scriptable.GetSignalSource(name, building));
  }

  /// <inheritdoc cref="IScriptable.GetSignalDefinition"/>
  public SignalDef GetSignalDefinition(string name, BaseComponent building) {
    return ExecuteOnRegisteredComponent(name, scriptable => scriptable.GetSignalDefinition(name, building));
  }

  /// <inheritdoc cref="IScriptable.GetActionNamesForBuilding"/>
  public string[] GetActionNamesForBuilding(BaseComponent building) {
    return _registeredScriptables.Values
        .SelectMany(s => s.GetActionNamesForBuilding(building))
        .ToArray();
  }

  /// <inheritdoc cref="IScriptable.GetActionExecutor"/>
  public Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    return ExecuteOnRegisteredComponent(name, scriptable => scriptable.GetActionExecutor(name, building));
  }

  /// <inheritdoc cref="IScriptable.GetActionDefinition"/>
  public ActionDef GetActionDefinition(string name, BaseComponent building) {
    return ExecuteOnRegisteredComponent(name, scriptable => scriptable.GetActionDefinition(name, building));
  }

  /// <inheritdoc cref="IScriptable.RegisterSignalChangeCallback"/>
  public void RegisterSignalChangeCallback(string name, AutomationBehavior building, Action onValueChanged) {
    ExecuteOnRegisteredComponent(
        name, scriptable => scriptable.RegisterSignalChangeCallback(name, building, onValueChanged));
  }

  /// <inheritdoc cref="IScriptable.UnregisterSignalChangeCallback"/>
  public void UnregisterSignalChangeCallback(string name, AutomationBehavior building, Action onValueChanged) {
    ExecuteOnRegisteredComponent(
        name, scriptable => scriptable.UnregisterSignalChangeCallback(name, building, onValueChanged));
  }

  /// <inheritdoc cref="IScriptable.InstallAction"/>
  public void InstallAction(string name, BaseComponent building) {
    ExecuteOnRegisteredComponent(name, scriptable => scriptable.InstallAction(name, building));
  }

  /// <inheritdoc cref="IScriptable.UninstallAction"/>
  public void UninstallAction(string name, BaseComponent building) {
    ExecuteOnRegisteredComponent(name, scriptable => scriptable.UninstallAction(name, building));
  }

  #endregion

  #region Implementation

  readonly Dictionary<string, IScriptable> _registeredScriptables = [];

  void ExecuteOnRegisteredComponent(string name, Action<IScriptable> action) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    action(scriptable);
  }

  T ExecuteOnRegisteredComponent<T>(string name, Func<IScriptable,T> action) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return action(scriptable);
  }

  #endregion
}
