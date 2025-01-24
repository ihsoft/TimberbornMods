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

  #region IScriptable implementation

  /// <inheritdoc/>
  public override string Name => "Floodgate";

  /// <inheritdoc/>
  public override Type InstanceType => typeof(Floodgate);

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(BaseComponent building) {
    return [SetHeightActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent instance) {
    var floodgate = instance as Floodgate;
    return name switch {
        SetHeightActionName => args => SetHeight(floodgate, args),
        _ => throw new ScriptError("Unknown action: " + name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, BaseComponent instance) {
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

  static void SetHeight(Floodgate floodgate, ScriptValue[] args) {
    AssertActionArgsCount(SetHeightActionName, args, 1);
    var height = args[0].AsNumber / 100f;
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
