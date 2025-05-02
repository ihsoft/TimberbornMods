// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Parser;
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
    return GetScriptable(name).GetSignalSource(name, building);
  }

  /// <inheritdoc cref="IScriptable.GetSignalDefinition"/>
  public SignalDef GetSignalDefinition(string name, BaseComponent building) {
    return GetScriptable(name).GetSignalDefinition(name, building);
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
    return GetScriptable(name).GetActionExecutor(name, building);
  }

  /// <inheritdoc cref="IScriptable.GetActionDefinition"/>
  public ActionDef GetActionDefinition(string name, BaseComponent building) {
    return GetScriptable(name).GetActionDefinition(name, building);
  }

  /// <inheritdoc cref="IScriptable.RegisterSignalChangeCallback"/>
  public void RegisterSignals(IExpression expression, ISignalListener host) {
    var signalNames = new HashSet<string>();
    expression.VisitNodes(x => {
      if (x is SignalOperatorExpr signal) {
        signalNames.Add(signal.SignalName);
      }
    });
    foreach (var signalName in signalNames) {
      GetScriptable(signalName).RegisterSignalChangeCallback(signalName, host);
    }
  }

  /// <inheritdoc cref="IScriptable.UnregisterSignalChangeCallback"/>
  public void UnregisterSignals(IExpression expression, ISignalListener host) {
    var signalNames = new HashSet<string>();
    expression.VisitNodes(x => {
      if (x is SignalOperatorExpr signal) {
        signalNames.Add(signal.SignalName);
      }
    });
    foreach (var signalName in signalNames) {
      GetScriptable(signalName).UnregisterSignalChangeCallback(signalName, host);
    }
  }

  /// <inheritdoc cref="IScriptable.InstallAction"/>
  public void InstallActions(IExpression expression, BaseComponent building) {
    expression.VisitNodes(x => {
      if (x is ActionExpr action) {
        GetScriptable(action.ActionName).InstallAction(action.ActionName, building);
      }
    });
  }

  /// <inheritdoc cref="IScriptable.UninstallAction"/>
  public void UninstallActions(IExpression expression, BaseComponent building) {
    expression.VisitNodes(x => {
      if (x is ActionExpr action) {
        GetScriptable(action.ActionName).UninstallAction(action.ActionName, building);
      }
    });
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

  IScriptable GetScriptable(string name) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError.ParsingError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable;
  }

  #endregion
}
