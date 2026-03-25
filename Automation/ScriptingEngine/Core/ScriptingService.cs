// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
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
  /// <remarks>
  /// Don't waste loading time and memory by injecting it. Use directly! However, avoid doing so from the singletons
  /// constructors and dependency injectors as the instantiation order is undetermined.
  /// </remarks>
  public static ScriptingService Instance { get; private set; }

  /// <summary>Signal callback wrapper.</summary>
  record SignalCallback(string Name, ISignalListener SignalListener) {
    /// <inheritdoc/>
    public override string ToString() {
      var host = SignalListener is IAutomationCondition
          ? SignalListener.ToString()
          : SignalListener.GetHashCode().ToString();
      return string.Format("SignalCallback(Host={0},OwnerBuilding={1},Name={2},Rule=#{3})",
                           host, DebugEx.ObjectToString(SignalListener.Behavior), Name, GetScriptingRuleIndex() + 1);
    }

    /// <summary>Find the index of teh rule that golds the signal listener.</summary>
    /// <returns>The 0-based index or -1 if the listener is not a scripted condtion.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public int GetScriptingRuleIndex() {
      if (SignalListener is not IAutomationCondition condition) {
        return -1;
      }
      for (var i = 0; i < condition.Behavior.Actions.Count; i++) {
        if (condition.Behavior.Actions[i].Condition == condition) {
          return i;
        }
      }
      return -2;
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

  /// <summary>
  /// Triggers update on the signal, handling the expected errors. No scripting errors will be passed out.
  /// </summary>
  /// <remarks>
  /// It doesn't fail if a scripting error happen. It will report the problem, but the caller will never know if there
  /// was a problem. This implies that all signal handlers must handle the error "in place".
  /// </remarks>
  /// <param name="signalName"></param>
  /// <param name="listener"></param>
  internal void NotifySignalListener(string signalName, ISignalListener listener) {
    var signalCallback = new SignalCallback(signalName, listener);
    if (AutomationDebugSettings.LogSignalsPropagating) {
      DebugEx.Fine("Executing signal callback: {0}", signalCallback);
    }
    PushToExecutionStack(signalCallback);
    listener.OnValueChanged(signalName);
    PopFromExecutionStack(signalCallback);
  }

  public int GetExecutionStackSize() => _executionStack.Count;

  /// <summary>Places an execution trace object to the execution stack.</summary>
  /// <remarks>
  /// <p>
  /// The object can be anything. Its string representation will be used in the error reports. Some well known objects
  /// has a better support than the others. See <see cref="CaptureCallStack"/> method.
  /// </p>
  /// <p>
  /// The <paramref name="traceObject"/> is added to the stack <i>before</i> preforming any checks. That being said, the
  /// caller is responsible to remove it from the stack if any error raises.
  /// </p>
  /// </remarks>
  /// <param name="traceObject">The object to put at the top of the stack.</param>
  /// <seealso cref="PopFromExecutionStack"/>
  public void PushToExecutionStack(object traceObject) {
    _executionStack.Push(traceObject);
  }

  /// <summary>Reduces the execution stack by removing the specified object from the top.</summary>
  /// <param name="traceObject">The object to remove from the top of the stack.</param>
  /// <exception cref="InvalidOperationException">
  /// if the top of the stack doesn't have the object or the stack is empty. It's a programming error. This must never
  /// happen in the game. If you see this error, then the mod's code is broken.
  /// </exception>
  public void PopFromExecutionStack(object traceObject) {
    if (_executionStack.Count == 0) {
      throw new InvalidOperationException($"Cannot pop {0}. Script execution stack is empty.");
    }
    if (!ReferenceEquals(_executionStack.Peek(), traceObject)) {
      throw new InvalidOperationException(
          $"Script execution stack is corrupted. Expected to pop {traceObject}, but found {_executionStack.Peek()}");
    }
    _executionStack.Pop();
  }

  /// <summary>Gathers the execution stack and returns records as human-readable lines.</summary>
  public string[] CaptureCallStack() {
    var lines = new List<string>();
    foreach (var trace in _executionStack) {
      string line;
      if (trace is SignalCallback signalCallback) {
        var host = DebugEx.ObjectToString(signalCallback.SignalListener.Behavior);
        var ruleIndex = signalCallback.GetScriptingRuleIndex();
        line = ruleIndex == -1
            ? $"{host}: SignalName={signalCallback.Name}"
            : $"{host}, rule #{ruleIndex + 1}: SignalName={signalCallback.Name}";
      } else if (trace is ScriptedAction scriptedAction) {
        var host = DebugEx.ObjectToString(scriptedAction.Behavior);
        var ruleIndex = scriptedAction.Behavior.Actions.IndexOf(scriptedAction);
        line = $"{host}, rule #{ruleIndex + 1}: {scriptedAction.Expression}";
      } else {
        line = trace.ToString();
      }
      lines.Add(line);
    }
    return lines.ToArray();
  }

  #endregion

  #region Implementation

  readonly Dictionary<string, IScriptable> _registeredScriptables = [];
  readonly Stack<object> _executionStack = new(50);

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
