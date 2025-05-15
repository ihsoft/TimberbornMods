// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.WaterBuildings;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class FloodgateScriptableComponent : ScriptableComponentBase {

  const string HeightSignalLocKey = "IgorZ.Automation.Scriptable.Floodgate.Signal.Height";
  const string SetHeightActionLocKey = "IgorZ.Automation.Scriptable.Floodgate.Action.SetHeight";

  const string HeightSignalName = "Floodgate.Height";
  const string SetHeightActionName = "Floodgate.SetHeight";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Floodgate";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponentFast<Floodgate>() ? [HeightSignalName] : [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var floodgate = GetFloodgate(behavior);
    return name switch {
        HeightSignalName => () => ScriptValue.FromFloat(floodgate.Height),
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    return name switch {
        HeightSignalName => HeightSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponentFast<Floodgate>() ? [SetHeightActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var floodgate = GetFloodgate(behavior);
    return name switch {
        SetHeightActionName => args => SetHeightAction(floodgate, args),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
    return name switch {
        SetHeightActionName => SetHeightActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    host.Behavior.GetOrCreate<HeightChangeTracker>().AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    host.Behavior.GetOrThrow<HeightChangeTracker>().RemoveSignal(signalOperator, host);
  }

  #endregion

  #region Signals

  SignalDef HeightSignalDef => _heightSignalDef ??= new SignalDef {
      ScriptName = HeightSignalName,
      DisplayName = Loc.T(HeightSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueFormatter = x => x.AsFloat.ToString("0.00"),
      },
  };
  SignalDef _heightSignalDef;

  #endregion

  #region Actions

  ActionDef SetHeightActionDef => _setHeightActionDef ??= new ActionDef {
      ScriptName = SetHeightActionName,
      DisplayName = Loc.T(SetHeightActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.Number,
              ValueFormatter = x => x.AsFloat.ToString("0.00"),
          },
      ],
  };
  ActionDef _setHeightActionDef;

  static void SetHeightAction(Floodgate floodgate, ScriptValue[] args) {
    AssertActionArgsCount(SetHeightActionName, args, 1);
    floodgate.SetHeight(args[0].AsFloat);
  }

  #endregion

  #region Implementation

  static Floodgate GetFloodgate(AutomationBehavior behavior) {
    var floodgate = behavior.GetComponentFast<Floodgate>();
    if (!floodgate) {
      throw new ScriptError.BadStateError(behavior, "Floodgate component not found");
    }
    return floodgate;
  }

  #endregion

  #region Inventory change tracker component

  internal sealed class HeightChangeTracker : AbstractStatusTracker {
    Floodgate _floodgate;
    int _currentValue;

    void Start() {
      _floodgate = GetComponentFast<Floodgate>();
      _currentValue = Mathf.RoundToInt(_floodgate.Height * 100f);
    }

    public void OnHeighChanged() {
      if (!_floodgate) {
        return;
      }
      var newValue = Mathf.RoundToInt(_floodgate.Height * 100f);
      if (_currentValue != newValue) {
        _currentValue = newValue;
        ScheduleSignal(HeightSignalName);
      }
    }
  }

  #endregion
}
