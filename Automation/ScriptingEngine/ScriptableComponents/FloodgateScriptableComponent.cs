// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class FloodgateScriptableComponent : ScriptableComponentBase {

  const string SetHeightActionLocKey = "IgorZ.Automation.Scriptable.Floodgate.Action.SetHeight";

  const string SetHeightActionName = "Floodgate.SetHeight";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Floodgate";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponentFast<Floodgate>() ? [SetHeightActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var floodgate = behavior.GetComponentFast<Floodgate>();
    if (!floodgate) {
      throw new ScriptError.BadStateError(behavior, "Floodgate component not found");
    }
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

  #endregion

  #region Actions

  ActionDef SetHeightActionDef => _setHeightActionDef ??= new ActionDef {
      ScriptName = SetHeightActionName,
      DisplayName = Loc.T(SetHeightActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.Number,
              NumberFormat = "0.00",
          },
      ],
  };
  ActionDef _setHeightActionDef;

  static void SetHeightAction(Floodgate floodgate, ScriptValue[] args) {
    AssertActionArgsCount(SetHeightActionName, args, 1);
    var height = args[0].AsFloat;
    if (height < 0) {
      height = floodgate.MaxHeight + height;
    }
    floodgate.SetHeight(height);
  }

  #endregion
}
