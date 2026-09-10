// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.Fields;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class FarmHouseScriptableComponent : ScriptableComponentBase {

  const string SetModeActionName = "FarmHouse.SetMode";
  const string SetModeActionLocKey = "IgorZ.Automation.Scriptable.FarmHouse.Action.SetMode";
  const string PlantingLocKey = "Fields.Planting";
  const string HarvestingLocKey = "Fields.Harvesting";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "FarmHouse";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<FarmHouse>() ? [SetModeActionName] : [];
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    if (name != SetModeActionName) {
      throw new UnknownActionException(name);
    }
    GetComponentOrThrow<FarmHouse>(behavior);
    return new ActionDef {
        ScriptName = name,
        DisplayName = Loc.T(SetModeActionLocKey),
        Arguments = [
            new ValueDef {
                ValueType = ScriptValue.TypeEnum.String,
                Options = [
                    new() { Value = "Planting", Text = Loc.T(PlantingLocKey) },
                    new() { Value = "Harvesting", Text = Loc.T(HarvestingLocKey) },
                ],
            },
        ],
    };
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    if (name != SetModeActionName) {
      throw new UnknownActionException(name);
    }
    var farmHouse = GetComponentOrThrow<FarmHouse>(behavior);
    return args => SetModeAction(args, farmHouse);
  }

  #endregion

  #region Actions

  static void SetModeAction(ScriptValue[] args, FarmHouse farmHouse) {
    AssertActionArgsCount(SetModeActionName, args, 1);
    switch (args[0].AsString) {
      case "Planting":
        farmHouse.PrioritizePlanting();
        break;
      case "Harvesting":
        farmHouse.UnprioritizePlanting();
        break;
      default:
        throw new ScriptError.BadValue("Unsupported farm mode: " + args[0].AsString);
    }
  }

  #endregion
}
