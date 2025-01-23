// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class FloodgateScriptableComponent : ScriptableComponentBase {

  const string SetHeightActionLocKey = "IgorZ.Automation.Scriptable.Floodgate.Action.SetHeight";

  const string SetHeightActionName = "SetHeight";

  #region IScriptable implementation

  /// <inheritdoc/>
  public override string Name => "Floodgate";

  /// <inheritdoc/>
  public override Type InstanceType => typeof(Floodgate);

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(BaseComponent building) {
    return [$"{Name}.{SetHeightActionName}"];
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
      FullName = $"{Name}.{SetHeightActionName}",
      DisplayName = SetHeightActionLocKey,
      ArgumentTypes = [
          new ArgumentDef {
              ValueType = ScriptValue.TypeEnum.Number,
              Format = "0.00",
          },
      ],
  };
  ActionDef? _setHeightActionDef;

  static void SetHeight(Floodgate floodgate, ScriptValue[] args) {
    AssertArgsCount(SetHeightActionName, args, 1);
    floodgate.SetHeight(args[0].AsNumber / 100f);
  }

  #endregion
}
