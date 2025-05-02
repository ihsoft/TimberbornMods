// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Service that provides access to the scripting engine.</summary>
sealed class ScriptingService {

  #region API

  /// <summary>Signal callback wrapper.</summary>
  public readonly record struct SignalCallback(string Name, ISignalListener SignalListener) {
    public override string ToString() =>
        string.Format("SignalCallback(Host={0},OwnerBuilding={1}, Name={2})",
                      SignalListener.GetHashCode(), DebugEx.ObjectToString(SignalListener.Behavior), Name);
  }

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

  public Func<object> GetPropertySource(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError.ParsingError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetPropertySource(name, building);
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
  public void RegisterSignalChangeCallback(string name, ISignalListener host) {
    ExecuteOnRegisteredComponent(name, scriptable => scriptable.RegisterSignalChangeCallback(name, host));
  }

  /// <inheritdoc cref="IScriptable.UnregisterSignalChangeCallback"/>
  public void UnregisterSignalChangeCallback(string name, ISignalListener host) {
    ExecuteOnRegisteredComponent(name, scriptable => scriptable.UnregisterSignalChangeCallback(name, host));
  }

  /// <inheritdoc cref="IScriptable.InstallAction"/>
  public void InstallAction(string name, BaseComponent building) {
    ExecuteOnRegisteredComponent(name, scriptable => scriptable.InstallAction(name, building));
  }

  /// <inheritdoc cref="IScriptable.UninstallAction"/>
  public void UninstallAction(string name, BaseComponent building) {
    ExecuteOnRegisteredComponent(name, scriptable => scriptable.UninstallAction(name, building));
  }

  internal void ScheduleSignalCallback(SignalCallback callback) {
    if (_callbackStack.Contains(callback)) {
      HostedDebugLog.Error(callback.SignalListener.Behavior, "Circular execution of signal '{0}'. Execution log:\n{1}",
                           callback.Name, GetExecutionLog());
      throw new ScriptError.RuntimeError($"Circular execution of signal '{callback.Name}'");
    }
    try {
      DebugEx.Fine("Executing signal callback: {0}", callback);
      _callbackStack.Push(callback);
      callback.SignalListener.OnValueChanged(callback.Name);
    } finally {
      _callbackStack.Pop();
    }
  }

  #endregion

  #region Implementation

  readonly Dictionary<string, IScriptable> _registeredScriptables = [];
  readonly Stack<SignalCallback> _callbackStack = new();

  string GetExecutionLog() {
    return string.Join("\n", _callbackStack.Select(x => $"{DebugEx.ObjectToString(x.SignalListener.Behavior)}:{x.Name}"));
  }

  void ExecuteOnRegisteredComponent(string name, Action<IScriptable> action) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError.ParsingError("Unknown scriptable component: " + nameItems[0]);
    }
    action(scriptable);
  }

  T ExecuteOnRegisteredComponent<T>(string name, Func<IScriptable,T> action) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError.ParsingError("Unknown scriptable component: " + nameItems[0]);
    }
    return action(scriptable);
  }

  #endregion
}
