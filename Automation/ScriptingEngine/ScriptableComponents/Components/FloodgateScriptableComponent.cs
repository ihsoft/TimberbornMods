// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.WaterBuildings;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

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
    return behavior.GetComponent<Floodgate>() ? [HeightSignalName] : [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var floodgate = GetComponentOrThrow<Floodgate>(behavior);
    return name switch {
        HeightSignalName => () => HeightSignal(floodgate),
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    var floodgate = GetComponentOrThrow<Floodgate>(behavior);
    return name switch {
        HeightSignalName => _signalDefsCache.GetOrAdd(name, floodgate.MaxHeight, MakeHeightSignalDef),
        _ => throw new UnknownSignalException(name),
    };
  }
  readonly ObjectsCache<SignalDef> _signalDefsCache = new();

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<Floodgate>() ? [SetHeightActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var floodgate = GetComponentOrThrow<Floodgate>(behavior);
    return name switch {
        SetHeightActionName => args => SetHeightAction(floodgate, args),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    var floodgate = GetComponentOrThrow<Floodgate>(behavior);
    return name switch {
        SetHeightActionName => _actionDefsCache.GetOrAdd(name, floodgate.MaxHeight, MakeSetActionDef),
        _ => throw new UnknownActionException(name),
    };
  }
  readonly ObjectsCache<ActionDef> _actionDefsCache = new();

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

  SignalDef MakeHeightSignalDef(string signalName, int maxHeight) {
    return new SignalDef {
        ScriptName = HeightSignalName,
        DisplayName = Loc.T(HeightSignalLocKey),
        Result = new ValueDef {
            ValueType = ScriptValue.TypeEnum.Number,
            DisplayNumericFormat = ValueDef.NumericFormatEnum.Float,
            DisplayNumericFormatRange = (0, maxHeight),
        },
    };
  }

  static ScriptValue HeightSignal(Floodgate floodgate) {
    return ScriptValue.FromFloat(floodgate.Height);
  }

  #endregion

  #region Actions

  ActionDef MakeSetActionDef(string actionName, int maxHeight) {
    return new ActionDef {
        ScriptName = SetHeightActionName,
        DisplayName = Loc.T(SetHeightActionLocKey),
        Arguments = [
            new ValueDef {
                ValueType = ScriptValue.TypeEnum.Number,
                DisplayNumericFormat = ValueDef.NumericFormatEnum.Float,
                DisplayNumericFormatRange = (0, maxHeight),
                RuntimeValueValidator = ValueDef.RangeCheckValidator(min: 0, max: maxHeight),
            },
        ],
    };
  }

  static void SetHeightAction(Floodgate floodgate, ScriptValue[] args) {
    AssertActionArgsCount(SetHeightActionName, args, 1);
    var currentHeight = ScriptValue.FromFloat(floodgate.Height);
    if (args[0] != currentHeight) {
      floodgate.SetHeight(args[0].AsFloat);
    }
  }

  #endregion

  #region Inventory change tracker component

  internal sealed class HeightChangeTracker : AbstractStatusTracker {
    Floodgate _floodgate;
    int _currentValue;

    /// <inheritdoc/>
    public override void Start() {
      base.Start();
      _floodgate = AutomationBehavior.GetComponentOrFail<Floodgate>();
      _currentValue = Mathf.RoundToInt(_floodgate.Height * 100f);
    }

    public void OnHeighChanged() {
      if (!_floodgate) {
        return;
      }
      var newValue = Mathf.RoundToInt(_floodgate.Height * 100f);
      if (_currentValue != newValue) {
        _currentValue = newValue;
        ScheduleSignal(HeightSignalName, ignoreErrors: true);
      }
    }
  }

  #endregion
}
