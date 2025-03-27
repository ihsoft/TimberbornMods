// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BaseComponentSystem;
using Timberborn.WaterBuildings;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class FloodgateScriptableComponent : ScriptableComponentBase {

  const string SetHeightActionLocKey = "IgorZ.Automation.Scriptable.Floodgate.Action.SetHeight";

  const string SetHeightActionName = "Floodgate.SetHeight";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Floodgate";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(BaseComponent building) {
    return building.GetComponentFast<Floodgate>() ? [SetHeightActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    var floodgate = building.GetComponentFast<Floodgate>();
    if (!floodgate) {
      throw new ScriptError("Floodgate component not found");
    }
    return name switch {
        SetHeightActionName => args => SetHeightAction(floodgate, args),
        _ => throw new ScriptError("Unknown action: " + name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, BaseComponent _) {
    return name switch {
        SetHeightActionName => SetHeightActionDef,
        _ => throw new ScriptError("Unknown action: " + name),
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
              FormatNumber = FormatHeight,
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

  static string FormatHeight(int value) {
    var floodgate = ExpressionParser.Instance.CurrentParserContext.ScriptHost.GetComponentFast<Floodgate>();
    var height = value / 100f;
    if (height < 0) {
      height = floodgate.MaxHeight + height;
    }
    height = Mathf.Clamp(height, 0, floodgate.MaxHeight);
    return height.ToString("0.00");
  }

  #endregion
}
