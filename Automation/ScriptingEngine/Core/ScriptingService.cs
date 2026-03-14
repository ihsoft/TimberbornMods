// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.Automation.Settings;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.Core;

/// <summary>Service that provides access to the scripting engine.</summary>
sealed class ScriptingService {

  #region API

  /// <summary>The scripting service instance shortcut.</summary>
  /// <remarks>Don't waste loading time and memory by injecting it. Use directly!</remarks>
  public static ScriptingService Instance { get; private set; }

  /// <summary>Signal callback wrapper.</summary>
  public readonly record struct SignalCallback(string Name, ISignalListener SignalListener) {
    /// <inheritdoc/>
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

  /// <summary>Registers callbacks for all signals in the expression to the provided host.</summary>
  /// <remarks>The host will get exactly one notification per signal name.</remarks>
  /// <returns>
  /// The list of signal operators that were requesting the callback. This information will be needed to unregister the
  /// callbacks.
  /// </returns>
  /// <seealso cref="UnregisterSignals"/>
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
  /// <seealso cref="RegisterSignals"/>
  public void UnregisterSignals(IList<SignalOperator> signalOperators, ISignalListener host) {
    foreach (var signal in signalOperators) {
      GetScriptable(signal.SignalName, throwErrors: true).UnregisterSignalChangeCallback(signal, host);
    }
  }

  /// <summary>Installs the actions from the provided expression.</summary>
  /// <returns>The list of actions that were installed. This information will be needed to uninstall the
  /// actions.
  /// </returns>
  /// <seealso cref="UninstallActions"/>
  public List<ActionOperator> InstallActions(IExpression expression, AutomationBehavior behavior) {
    var actions = new List<ActionOperator>();
    expression.VisitNodes(x => {
      if (x is ActionOperator action) {
        actions.Add(action);
        GetScriptable(action.ActionName, throwErrors: true).InstallAction(action, behavior);
      }
    });
    return actions;
  }

  /// <inheritdoc cref="IScriptable.UninstallAction"/>
  /// <seealso cref="InstallActions"/>
  public void UninstallActions(IList<ActionOperator> actionOperators, AutomationBehavior behavior) {
    foreach (var actionOperator in actionOperators) {
      GetScriptable(actionOperator.ActionName, throwErrors: true).UninstallAction(actionOperator, behavior);
    }
  }

  internal void ScheduleSignalCallback(SignalCallback callback, bool ignoreErrors = false) {
    if (AutomationDebugSettings.LogSignalsPropagating) {
      DebugEx.Fine("Executing signal callback: {0}", callback);
    }
    _callbackStack.Push(callback);
    
    if (_callbackStack.Count > ScriptEngineSettings.SignalExecutionStackSize) {
      var stackTrace =_callbackStack.Select(x => $"{DebugEx.ObjectToString(x.SignalListener.Behavior)}:{x.Name}");
      HostedDebugLog.Error(callback.SignalListener.Behavior, "Script stack overflow ({0}). Execution log:\n{1}",
                           ScriptEngineSettings.SignalExecutionStackSize, string.Join("\n", stackTrace));
      throw new ScriptError.InternalError("Script stack overflow");
    }
    try {
      callback.SignalListener.OnValueChanged(callback.Name);
    } catch (ScriptError.RuntimeError e) {
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

  readonly Dictionary<string, IScriptable> _registeredScriptables = [];
  readonly Stack<SignalCallback> _callbackStack = new();

  ScriptingService() {
    Instance = this;
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
