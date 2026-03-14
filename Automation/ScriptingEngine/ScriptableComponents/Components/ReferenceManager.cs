// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

/// <summary>Helper class to track the registration of signals and actions.</summary>
sealed class ReferenceManager(ScriptingService scriptingService) {
  public readonly Dictionary<ISignalListener, HashSet<SignalOperator>> Signals = new();
  public readonly HashSet<ActionOperator> Actions = [];

  /// <summary>Registers a new action. The action must be unique.</summary>
  public void AddAction(ActionOperator actionOperator) {
    if (!Actions.Add(actionOperator)) {
      throw new InvalidOperationException("Action already added: " + actionOperator);
    }
  }

  /// <summary>Removes an action.</summary>
  public void RemoveAction(ActionOperator actionOperator) {
    if (!Actions.Remove(actionOperator)) {
      throw new InvalidOperationException("Action not found: " + actionOperator);
    }
  }

  /// <summary>Adds signal to the given host. The host will be notified when the signal is scheduled.</summary>
  /// <seealso cref="ScheduleSignal"/>
  public void AddSignal(SignalOperator signalOperator, ISignalListener host) {
    if (!Signals.TryGetValue(host, out var listeners)) {
      listeners = [];
      Signals.Add(host, listeners);
    }
    if (!listeners.Add(signalOperator)) {
      throw new InvalidOperationException("Signal already registered: " + signalOperator);
    }
  }

  /// <summary>Removes a signal notification from the given host.</summary>
  public void RemoveSignal(SignalOperator signalOperator, ISignalListener host) {
    if (!Signals.TryGetValue(host, out var listeners) || !listeners.Remove(signalOperator)) {
      throw new InvalidOperationException("Signal not registered: " + signalOperator);
    }
    if (listeners.Count == 0) {
      Signals.Remove(host);
    }
  }

  /// <summary>Schedules all signals for the given signal name.</summary>
  /// <remarks>If the host has registered to the same signal multiple times, it will be notified once.</remarks>
  public void ScheduleSignal(string signalName, bool ignoreErrors = false) {
    var listeners = Signals
        .Where(pair => pair.Value.Any(x => x.SignalName == signalName))
        .Select(x => x.Key)
        .ToArray();  // Need a copy! The Signals dictionary can be modified in the loop.
    foreach (var listener in listeners) {
      scriptingService.ScheduleSignalCallback(new ScriptingService.SignalCallback(signalName, listener), ignoreErrors);
    }
  }
}
