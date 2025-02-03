// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class DebugScriptableComponent : ScriptableComponentBase {

  const string LogActionLocKey = "IgorZ.Automation.Scriptable.Debug.Action.Log";

  const string LogActionName = "Debug.Log";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Debug";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(BaseComponent _) {
    return [LogActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    return name switch {
        LogActionName => args => LogAction(building, args),
        _ => throw new ScriptError("Unknown action: " + name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, BaseComponent _) {
    return name switch {
        LogActionName => LogActionDef,
        _ => throw new ScriptError("Unknown action: " + name),
    };
  }

  #endregion

  #region Actions

  ActionDef LogActionDef => _logActionDef ??= new ActionDef {
      ScriptName = LogActionName,
      DisplayName = Loc.T(LogActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.String,
          },
      ],
  };
  ActionDef _logActionDef;

  static void LogAction(BaseComponent instance, ScriptValue[] args) {
    AssertActionArgsCount(LogActionName, args, 1);
    HostedDebugLog.Info(instance, "[Debug action]: {0}", args[0]);
  }

  #endregion
}
