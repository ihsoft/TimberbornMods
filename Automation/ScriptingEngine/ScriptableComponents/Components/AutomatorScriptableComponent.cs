// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.Automation;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class AutomatorScriptableComponent : ScriptableComponentBase {

  const string StateSignalLocKey = "IgorZ.Automation.Scriptable.Automator.Signal.State";

  const string StateSignalName = "Automator.State";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Automator";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    var automator = behavior.GetComponent<Automator>();
    if (automator && automator.IsTransmitter) {
      return [StateSignalName];
    }
    return [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var automator = GetComponentOrThrow<Automator>(behavior);
    return name switch {
        StateSignalName => () => StateSignal(automator),
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    GetComponentOrThrow<Automator>(behavior);  // Verify only.
    return name switch {
        StateSignalName => StateSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName is not StateSignalName) {
      throw new InvalidOperationException($"Unknown signal: {signalOperator.SignalName}");
    }
    host.Behavior.GetOrCreate<AutomatorStateTracker>().AddSignal(signalOperator, host);
  }
  
  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName is not StateSignalName) {
      throw new InvalidOperationException($"Unknown signal: {signalOperator.SignalName}");
    }
    host.Behavior.GetOrThrow<AutomatorStateTracker>().RemoveSignal(signalOperator, host);
  }

  #endregion

  #region Signals

  SignalDef StateSignalDef => _stateSignalDef ??= new SignalDef {
      ScriptName = StateSignalName,
      DisplayName = Loc.T(StateSignalLocKey),
      Result = new ValueDef {
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
          ValueType = ScriptValue.TypeEnum.Number,
      },
  };
  SignalDef _stateSignalDef;

  static ScriptValue StateSignal(Automator automator) {
    return ScriptValue.FromInt(automator.State == AutomatorState.On ? 1 : 0);
  }

  #endregion

  #region Tracker for the Automator state change

  internal sealed class AutomatorStateTracker : AbstractStatusTracker {
    public void OnStateChanged() {
      TriggerSignalUpdate(StateSignalName);
    }
  }

  #endregion
}
