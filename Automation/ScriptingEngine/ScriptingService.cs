// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.Settings;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Service that provides access to the scripting engine.</summary>
sealed class ScriptingService {

  #region API

  /// <summary>Signal callback wrapper.</summary>
  public readonly record struct SignalCallback(string Name, ISignalListener SignalListener) {
    public override string ToString() {
      var host = SignalListener is IAutomationCondition
          ? SignalListener.ToString()
          : SignalListener.GetHashCode().ToString();
      return string.Format("SignalCallback(Host={0},OwnerBuilding={1}, Name={2})",
                           host, DebugEx.ObjectToString(SignalListener.Behavior), Name);
    }
  }

  /// <summary>Registers a new scriptable component.</summary>
  public void RegisterScriptable(IScriptable scriptable) {
    _registeredScriptables.Add(scriptable.Name, scriptable);
  }

  /// <inheritdoc cref="IScriptable.GetSignalNamesForBuilding"/>
  public string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return _registeredScriptables.Values
        .SelectMany(s => s.GetSignalNamesForBuilding(behavior))
        .ToArray();
  }

  /// <inheritdoc cref="IScriptable.GetSignalSource"/>
  public Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    return GetScriptable(name).GetSignalSource(name, behavior);
  }

  /// <inheritdoc cref="IScriptable.GetSignalDefinition"/>
  public SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    return GetScriptable(name).GetSignalDefinition(name, behavior);
  }

  /// <inheritdoc cref="IScriptable.GetPropertySource"/>
  public Func<object> GetPropertySource(string name, AutomationBehavior behavior) {
    var nameItems = name.Split('.');
    return _registeredScriptables.TryGetValue(nameItems[0], out var scriptable)
        ? scriptable.GetPropertySource(name, behavior)
        : null;
  }

  /// <inheritdoc cref="IScriptable.GetActionNamesForBuilding"/>
  public string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return _registeredScriptables.Values
        .SelectMany(s => s.GetActionNamesForBuilding(behavior))
        .ToArray();
  }

  /// <inheritdoc cref="IScriptable.GetActionExecutor"/>
  public Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    return GetScriptable(name).GetActionExecutor(name, behavior);
  }

  /// <inheritdoc cref="IScriptable.GetActionDefinition"/>
  public ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    return GetScriptable(name).GetActionDefinition(name, behavior);
  }

  /// <inheritdoc cref="IScriptable.RegisterSignalChangeCallback"/>
  public List<SignalOperator> RegisterSignals(IExpression expression, ISignalListener host) {
    var signals = new List<SignalOperator>();
    expression.VisitNodes(x => {
      if (x is SignalOperator signal) {
        signals.Add(signal);
        GetScriptable(signal.SignalName, throwErrors: true).RegisterSignalChangeCallback(signal, host);
      }
    });
    return signals;
  }

  /// <inheritdoc cref="IScriptable.UnregisterSignalChangeCallback"/>
  public void UnregisterSignals(IExpression expression, ISignalListener host) {
    expression.VisitNodes(x => {
      if (x is SignalOperator signal) {
        GetScriptable(signal.SignalName, throwErrors: true).UnregisterSignalChangeCallback(signal, host);
      }
    });
  }

  /// <inheritdoc cref="IScriptable.InstallAction"/>
  public void InstallActions(IExpression expression, AutomationBehavior behavior) {
    expression.VisitNodes(x => {
      if (x is ActionOperator action) {
        GetScriptable(action.ActionName, throwErrors: true).InstallAction(action, behavior);
      }
    });
  }

  /// <inheritdoc cref="IScriptable.UninstallAction"/>
  public void UninstallActions(IExpression expression, AutomationBehavior behavior) {
    expression.VisitNodes(x => {
      if (x is ActionOperator action) {
        GetScriptable(action.ActionName, throwErrors: true).UninstallAction(action, behavior);
      }
    });
  }

  internal void ScheduleSignalCallback(SignalCallback callback, bool ignoreErrors = false) {
    if (_debugSettings.LogSignalsPropagating.Value) {
      DebugEx.Fine("Executing signal callback: {0}", callback);
    }
    if (_callbackStack.Contains(callback)) {
      var log = new List<string>([$"{DebugEx.ObjectToString(callback.SignalListener.Behavior)}:{callback.Name}"]);
      log.AddRange(_callbackStack.Select(x => $"{DebugEx.ObjectToString(x.SignalListener.Behavior)}:{x.Name}"));
      HostedDebugLog.Error(callback.SignalListener.Behavior, "Circular execution of signal '{0}'. Execution log:\n{1}",
                           callback.Name, string.Join("\n", log));
      throw new ScriptError.RuntimeError($"Circular execution of signal '{callback.Name}'");
    }
    try {
      _callbackStack.Push(callback);
      callback.SignalListener.OnValueChanged(callback.Name);
    } catch (ScriptError e) {
      if (!ignoreErrors) {
        throw;
      }
      DebugEx.Error("Aborted execution of signal callback: {0}\n{1}", callback, e.Message);
    } finally {
      _callbackStack.Pop();
    }
  }

  #endregion

  #region Implementation

  readonly AutomationDebugSettings _debugSettings;

  readonly Dictionary<string, IScriptable> _registeredScriptables = [];
  readonly Stack<SignalCallback> _callbackStack = new();

  [Inject]
  ScriptingService(AutomationDebugSettings debugSettings) {
    _debugSettings = debugSettings;
  }

  internal IList<string> GetScriptableNames() {
    return _registeredScriptables.Keys.ToList();
  }

  IScriptable GetScriptable(string name, bool throwErrors = false) {
    var nameItems = name.Split('.');
    if (_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      return scriptable;
    }
    if (throwErrors) {
      throw new InvalidOperationException($"Unknown scriptable component: {nameItems[0]}");
    }
    throw new ScriptError.ParsingError("Unknown scriptable component: " + nameItems[0]);
  }

  #endregion
}
