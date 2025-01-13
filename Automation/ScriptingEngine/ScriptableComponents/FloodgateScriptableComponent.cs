// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class FloodgateScriptableComponent : ScriptableComponentBase {

  const string SetHeightActionName = "SetHeight";

  #region IScriptable implementation

  /// <inheritdoc/>
  public override string Name => "Floodgate";

  /// <inheritdoc/>
  public override Type InstanceType => typeof(Floodgate);

  /// <inheritdoc/>
  public override Action GetActionExecutor(string name, BaseComponent instance, string[] args) {
    var floodgate = instance as Floodgate;
    switch (name) {
      case SetHeightActionName:
        if (args.Length != 1) {
          throw new ScriptError($"{SetHeightActionName} action requires 1 argument");
        }
        var value = ParseFloat(args[0]);
        return () => floodgate!.SetHeight(value);
      default:
        throw new ScriptError("Unknown action: " + name);
    }
  }

  /// <inheritdoc/>
  public override IScriptable.ActionDef GetActionDefinition(string name, BaseComponent instance) {
    return name switch {
        SetHeightActionName => new IScriptable.ActionDef {
            FullName = $"{Name}.{SetHeightActionName}",
            DisplayName = LocAction(SetHeightActionName),
            ArgumentTypes = [
                new IScriptable.ArgumentDef {
                    ArgumentType = IScriptable.ArgumentDef.Type.Float,
                },
            ],
        },
        _ => throw new ScriptError("Unknown action: " + name)
    };
  }

  #endregion
}
