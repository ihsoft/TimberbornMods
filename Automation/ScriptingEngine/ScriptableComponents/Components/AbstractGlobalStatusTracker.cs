// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.Utils;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

/// <summary>Tracks persistent signal values once per world, independently of the buildings listening to them.</summary>
/// <remarks>
/// The owning singleton forwards load/save and supplies values after its source has settled. This class neither polls
/// sources nor schedules work. Restoring or initializing a value never notifies listeners.
/// </remarks>
abstract class AbstractGlobalStatusTracker(SingletonKey stateKey, ReferenceManager references) {
  /// <summary>Restores cached values before rules are bound, without consulting their sources.</summary>
  public virtual void Load(ISingletonLoader loader) {
    if (!loader.TryGetSingleton(stateKey, out var component)) {
      return;
    }
    var saved = component.GetValueOrDefault(SavedSignalsKey, null);
    if (saved == null) {
      return;
    }
    var state = StringProtoSerializer.Deserialize<SavedSignalValues>(saved);
    // Previously saved empty collections can deserialize without a SignalValues array.
    if (state?.SignalValues == null) {
      return;
    }
    foreach (var item in state.SignalValues) {
      _values.Add(item.SignalName, item.ToScriptValue());
    }
  }

  /// <summary>Saves last valid values, including values temporarily unavailable from their sources.</summary>
  public virtual void Save(ISingletonSaver saver) {
    var values = _values.Where(x => x.Value.ValueType != ScriptValue.TypeEnum.Unset)
        .Select(x => SavedSignalValues.SignalValue.FromScriptValue(x.Key, x.Value)).ToArray();
    if (values.Length == 0) {
      return;
    }
    var state = new SavedSignalValues { SignalValues = values };
    saver.GetSingleton(stateKey).Set(SavedSignalsKey, StringProtoSerializer.Serialize(state));
  }

  /// <summary>Reads the source only when no saved or previously observed value exists.</summary>
  public ScriptValue GetOrInitialize(string name, Func<ScriptValue> read) {
    if (!_values.TryGetValue(name, out var value)) {
      value = read();
      _values.Add(name, value);
    }
    return value;
  }

  /// <summary>Returns a cached value without evaluating its source.</summary>
  public bool TryGetSignalValue(string name, out ScriptValue value) => _values.TryGetValue(name, out value);

  /// <summary>Stores a value without notifying. Returns whether it differs from the previous value.</summary>
  public bool SetSignalValue(string name, ScriptValue value) {
    var changed = !_values.TryGetValue(name, out var previous) || previous != value;
    _values[name] = value;
    return changed;
  }

  /// <summary>Discards an unobserved value when its owner stops tracking changes to it.</summary>
  public void ForgetSignalValue(string name) => _values.Remove(name);

  /// <summary>Registers a listener without reading or changing the cached value.</summary>
  public void AddSignal(SignalOperator signal, ISignalListener host) => references.AddSignal(signal, host);

  /// <summary>Unregisters a listener. Returns whether any listeners for this signal remain.</summary>
  public bool RemoveSignal(SignalOperator signal, ISignalListener host) {
    references.RemoveSignal(signal, host);
    return HasSignalListeners(signal.SignalName);
  }

  /// <summary>Whether any rule observes this signal.</summary>
  public bool HasSignalListeners(string name) {
    return references.Signals.Values.Any(signals => signals.Any(x => x.SignalName == name));
  }

  /// <summary>Publishes a supplied value when changed, or forces notification on recovery from a read error.</summary>
  public void TriggerSignalUpdate(string name, ScriptValue value, bool forceNotify = false) {
    if (SetSignalValue(name, value) || forceNotify) {
      references.TriggerSignalUpdate(name);
    }
  }

  /// <summary>Notifies listeners of a read error without replacing the last valid value.</summary>
  public void TriggerSignalError(string name) => references.TriggerSignalUpdate(name);

  static readonly PropertyKey<string> SavedSignalsKey = new("SavedSignals");
  readonly Dictionary<string, ScriptValue> _values = new();
}
