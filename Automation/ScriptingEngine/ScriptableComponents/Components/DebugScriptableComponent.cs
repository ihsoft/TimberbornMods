// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class DebugScriptableComponent : ScriptableComponentBase {

  const string TickerSignalLocKey = "IgorZ.Automation.Scriptable.Debug.Signal.Ticker";
  const string LogStrActionLocKey = "IgorZ.Automation.Scriptable.Debug.Action.LogStr";
  const string LogNumActionLocKey = "IgorZ.Automation.Scriptable.Debug.Action.LogNum";
  const string LogActionLocKey = "IgorZ.Automation.Scriptable.Debug.Action.Log";

  const string TickerSignalName = "Debug.Ticker";
  const string LogStrActionName = "Debug.LogStr";
  const string LogNumActionName = "Debug.LogNum";
  const string LogActionName = "Debug.Log";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Debug";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    return name switch {
        TickerSignalName => TickerSignal,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    return name switch {
        TickerSignalName => TickerSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    _referenceManager.AddSignal(signalOperator, host);
    if (_referenceManager.Signals.Count == 1) {
      _automationService.RegisterTickable(OnTick);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    _referenceManager.RemoveSignal(signalOperator, host);
    if (_referenceManager.Signals.Count == 0) {
      _automationService.UnregisterTickable(OnTick);
    }
  }

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior _) {
    return [LogStrActionName, LogNumActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    return name switch {
        LogStrActionName => args => LogStrAction(behavior, args),
        LogNumActionName => args => LogNumAction(behavior, args),
        LogActionName => args => LogAction(behavior, args),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
    return name switch {
        LogStrActionName => LogStrActionDef,
        LogNumActionName => LogNumActionDef,
        LogActionName => LogActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

  #endregion

  #region Signals

  const int ReasonableTickQuantifierMax = 10;

  SignalDef TickerSignalDef => _tickerSignalDef ??= new SignalDef {
      ScriptName = TickerSignalName,
      DisplayName = Loc.T(TickerSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueUiHint = GetArgumentMaxValueHint(ReasonableTickQuantifierMax),
          RuntimeValueValidator = ValueDef.RangeCheckValidatorInt(min:0),
      },
  };
  SignalDef _tickerSignalDef;

  ScriptValue TickerSignal() {
    return ScriptValue.FromInt(AutomationService.CurrentTick);
  }

  #endregion

  #region Actions

  ActionDef LogStrActionDef => _logStrActionDef ??= new ActionDef {
      ScriptName = LogStrActionName,
      DisplayName = Loc.T(LogStrActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.String,
          },
      ],
  };
  ActionDef _logStrActionDef;

  ActionDef LogNumActionDef => _logNumActionDef ??= new ActionDef {
      ScriptName = LogNumActionName,
      DisplayName = Loc.T(LogNumActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.Number,
          },
      ],
  };
  ActionDef _logNumActionDef;

  ActionDef LogActionDef => _logActionDef ??= new ActionDef {
      ScriptName = LogActionName,
      DisplayName = Loc.T(LogActionLocKey),
      Arguments = [
          new ValueDef { ValueType = ScriptValue.TypeEnum.String },
      ],
      VarArg = new ValueDef { ValueType = ScriptValue.TypeEnum.Unset },
  };
  ActionDef _logActionDef;

  static void LogStrAction(BaseComponent instance, ScriptValue[] args) {
    AssertActionArgsCount(LogStrActionName, args, 1);
    HostedDebugLog.Info(instance, "[Debug Log]: {0}", args[0].AsString);
  }

  static void LogNumAction(BaseComponent instance, ScriptValue[] args) {
    AssertActionArgsCount(LogNumActionName, args, 1);
    HostedDebugLog.Info(instance, "[Debug Log]: {0}", args[0].AsNumber);
  }

  static void LogAction(BaseComponent instance, ScriptValue[] args) {
    if (args.Length < 1) {
      throw new ScriptError.ParsingError($"{LogActionName} action requires at least one argument");
    }
    var fmtArgs = new object[args.Length - 1];
    for (var i = 0; i < args.Length - 1; i++) {
      var arg = args[i + 1];
      fmtArgs[i] = arg.ValueType switch {
          ScriptValue.TypeEnum.String => arg.AsString,
          ScriptValue.TypeEnum.Number => arg.AsFloat,
          ScriptValue.TypeEnum.Unset => throw new InvalidOperationException($"Unexpected Unset value type : {arg}"),
          _ => throw new ArgumentOutOfRangeException(),
      };
    }
    HostedDebugLog.Info(instance, "[Debug Log]: " + args[0].AsString, fmtArgs);
  }

  #endregion

  #region Implemenation

  readonly AutomationService _automationService;
  readonly ReferenceManager _referenceManager;

  DebugScriptableComponent(AutomationService automationService, ReferenceManager referenceManager) {
    _automationService = automationService;
    _referenceManager = referenceManager;
  }

  void OnTick(int currentTick) {
    if (_referenceManager.Signals.Count > 0) {
      _referenceManager.ScheduleSignal(TickerSignalName, ignoreErrors: true);
    }
  }

  #endregion
}
