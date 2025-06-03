// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class DebugScriptableComponent : ScriptableComponentBase {

  const string LogStrActionLocKey = "IgorZ.Automation.Scriptable.Debug.Action.LogStr";
  const string LogNumActionLocKey = "IgorZ.Automation.Scriptable.Debug.Action.LogNum";

  const string LogStrActionName = "Debug.LogStr";
  const string LogNumActionName = "Debug.LogNum";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Debug";

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
}
