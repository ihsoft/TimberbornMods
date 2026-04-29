// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.Utils;
using ProtoBuf;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

/// <summary>A base component to track the signals and actions on an automation behavior.</summary>
abstract class AbstractStatusTracker : AbstractDynamicComponent, IPersistentEntity {

  #region IPersistentEntity implementation

  static readonly PropertyKey<string> SavedSignalsKey = new("SavedSignals");

  [ProtoContract]
  sealed record SavedSignalsState {
    [ProtoContract]
    public sealed record SignalValue {
      [ProtoMember(1)]
      public string SignalName { get; init; }
      [ProtoMember(2)]
      public string StrValue { get; init; }
      [ProtoMember(3)]
      public int NumValue { get; init; }

      public static SignalValue FromScriptValue(string signalName, ScriptValue value) {
        return new SignalValue {
            SignalName = signalName,
            StrValue = value.ValueType == ScriptValue.TypeEnum.String ? value.AsString : null,
            NumValue = value.ValueType == ScriptValue.TypeEnum.Number ? value.AsRawNumber : int.MinValue,
        };
      }

      public ScriptValue ToScriptValue() {
        return StrValue != null ? ScriptValue.FromString(StrValue) : ScriptValue.Of(NumValue);
      }
    }

    [ProtoMember(1)]
    public SignalValue[] SignalValues { get; init; }
  }

  ComponentKey EntityComponentKey => new(GetType().FullName);

  /// <inheritdoc/>
  public virtual void Save(IEntitySaver entitySaver) {
    var values = _signals
        .Where(x => x.Value.Listeners.Count > 0)
        .Select(kvp => SavedSignalsState.SignalValue.FromScriptValue(kvp.Key, kvp.Value.LastValue))
        .ToArray();
    if (values.Length == 0) {
      return;
    }
    var component = entitySaver.GetComponent(EntityComponentKey);
    var state = new SavedSignalsState {
        SignalValues = values,
    };
    component.Set(SavedSignalsKey, StringProtoSerializer.Serialize(state));
  }

  /// <inheritdoc/>
  public virtual void Load(IEntityLoader entityLoader) {
    if (!entityLoader.TryGetComponent(EntityComponentKey, out var component)) {
      return;
    }
    var state = StringProtoSerializer.Deserialize<SavedSignalsState>(component.Get(SavedSignalsKey));
    foreach (var signalValue in state.SignalValues) {
      _signals.Add(signalValue.SignalName, new SignalSink { LastValue = signalValue.ToScriptValue() });
    }
  }

  #endregion

  #region API

  /// <summary>True if the component has any actions.</summary>
  public bool HasActions => _actions.Count > 0;

  /// <summary>Adds signal to the given host. The host will be notified when the signal is scheduled.</summary>
  /// <seealso cref="TriggerSignalUpdate"/>
  /// <returns><c>true</c> if the first listener for the signal was added.</returns>
  public virtual bool AddSignal(SignalOperator signalOperator, ISignalListener host) {
    if (!_signals.TryGetValue(signalOperator.SignalName, out var signalSink)) {
      signalSink = new SignalSink {
          LastValue = signalOperator.ValueFn(),
      };
      _signals.Add(signalOperator.SignalName, signalSink);
    }
    var isFirstItem = signalSink.Listeners.Count == 0;
    signalSink.Register(signalOperator, host);
    return isFirstItem;
  }

  /// <summary>Removes a signal notification from the given host.</summary>
  /// <returns><c>true</c> if there are more listeners for the signal left.</returns>
  public virtual bool RemoveSignal(SignalOperator signalOperator, ISignalListener host) {
    if (!_signals.TryGetValue(signalOperator.SignalName, out var signalSink)) {
      throw new InvalidOperationException($"Signal listener not registered: {(signalOperator, host)}");
    }
    signalSink.Unregister(signalOperator, host);
    if (signalSink.Listeners.Count > 0) {
      return true;
    }
    _signals.Remove(signalOperator.SignalName);
    return false;
  }

  /// <summary>Registers a new action. The action must be unique.</summary>
  /// <returns><c>true</c> if the first action was added.</returns>
  public virtual bool AddAction(ActionOperator actionOperator) {
    if (!_actions.Add(actionOperator)) {
      throw new InvalidOperationException($"Action already added: {actionOperator}");
    }
    return _actions.Count == 1;
  }

  /// <summary>Removes an action.</summary>
  /// <returns><c>true</c> if there are more actions left.</returns>
  public virtual bool RemoveAction(ActionOperator actionOperator) {
    if (!_actions.Remove(actionOperator)) {
      throw new InvalidOperationException($"Action not found: {actionOperator}");
    }
    return _actions.Count > 0;
  }

  /// <summary>Notifies all signal listeners if the value has changed.</summary>
  /// <remarks>
  /// If the same host has registered to the same signal multiple times, it will be notified only once. If the value has
  /// not changed, no update will be triggered.
  /// </remarks>
  public void TriggerSignalUpdate(string signalName) {
    if (!_signals.TryGetValue(signalName, out var signalSink) || !signalSink.UpdateLastValue()) {
      return;
    }
    foreach (var listener in signalSink.Listeners) {
      ScriptingService.Instance.NotifySignalListener(signalName, listener);
    }
  }

  #endregion

  #region Imlementation

  sealed record SignalSink {
    public ScriptValue LastValue;
    public readonly List<ISignalListener> Listeners = [];

    readonly List<(SignalOperator signalOperator, ISignalListener listener)> _registrants = [];

    public bool UpdateLastValue() {
      var newValue = _registrants[0].signalOperator.ValueFn();
      var isChanged = LastValue != newValue;
      LastValue = newValue;
      return isChanged;
    }

    public void Register(SignalOperator signalOperator, ISignalListener listener) {
      var item = (signalOperator, listener);
      if (_registrants.Contains(item)) {
        throw new InvalidOperationException($"Signal listener already registered: {item}");
      }
      _registrants.Add(item);
      Listeners.Add(listener);
    }

    public void Unregister(SignalOperator signalOperator, ISignalListener listener) {
      var item = (signalOperator, listener);
      if (!_registrants.Remove(item)) {
        throw new InvalidOperationException($"Signal listener not registered: {item}");
      }
      Listeners.Clear();
      Listeners.AddRange(_registrants.Select(x => x.listener).Distinct());
    }
  }

  readonly Dictionary<string, SignalSink> _signals = new();
  readonly HashSet<ActionOperator> _actions = [];

  #endregion
}
