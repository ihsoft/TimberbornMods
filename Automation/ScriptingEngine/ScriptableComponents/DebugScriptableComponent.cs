// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class DebugScriptableComponent : ScriptableComponentBase {

  const string TickerSignalLocKey = "IgorZ.Automation.Scriptable.Debug.Signal.Ticker";
  const string LogStrActionLocKey = "IgorZ.Automation.Scriptable.Debug.Action.LogStr";
  const string LogNumActionLocKey = "IgorZ.Automation.Scriptable.Debug.Action.LogNum";

  const string TickerSignalName = "Debug.Ticker";
  const string LogStrActionName = "Debug.LogStr";
  const string LogNumActionName = "Debug.LogNum";

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

  readonly ReferenceManager _referenceManager = new ReferenceManager();

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
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
    return name switch {
        LogStrActionName => LogStrActionDef,
        LogNumActionName => LogNumActionDef,
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
          ValueValidator = ValueDef.RangeCheckValidatorInt(0, ReasonableTickQuantifierMax),
          ValueUiHint = GetArgumentMaxValueHint(ReasonableTickQuantifierMax),
      },
  };

  SignalDef _tickerSignalDef;

  ScriptValue TickerSignal() {
    return ScriptValue.Of(AutomationService.CurrentTick);
  }

  #endregion

  #region Actions

  ActionDef LogStrActionDef => _logActionDef ??= new ActionDef {
      ScriptName = LogStrActionName,
      DisplayName = Loc.T(LogStrActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.String,
          },
      ],
  };
  ActionDef _logActionDef;

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

  static void LogStrAction(BaseComponent instance, ScriptValue[] args) {
    AssertActionArgsCount(LogStrActionName, args, 1);
    HostedDebugLog.Info(instance, "[Debug action]: {0}", args[0].AsString);
  }

  static void LogNumAction(BaseComponent instance, ScriptValue[] args) {
    AssertActionArgsCount(LogNumActionName, args, 1);
    HostedDebugLog.Info(instance, "[Debug action]: {0}", args[0].AsNumber);
  }

  #endregion

  #region Implemenation

  AutomationService _automationService;

  [Inject]
  public void InjectDependencies(AutomationService automationService) {
    _automationService = automationService;
  }

  void OnTick(int currentTick) {
    //FIXME
    DebugEx.Warning("DebugScriptableComponent.OnTick called at tick {0}.", currentTick);
    _referenceManager.ScheduleSignal(TickerSignalName, ScriptingService, ignoreErrors: true);
  }

  #endregion
}
