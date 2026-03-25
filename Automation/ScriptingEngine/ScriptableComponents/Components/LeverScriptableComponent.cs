// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.AutomationBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

class LeverScriptableComponent : ScriptableComponentBase {
  
  const string SetStateActionLocKey = "IgorZ.Automation.Scriptable.Lever.Action.SetState";
  
  const string AutomationStateOn = "Automation.State.On";
  const string AutomationStateOff = "Automation.State.Off";

  const string SetStateActionName = "Lever.SetState";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Lever";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<Lever>() ? [SetStateActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var component = GetComponentOrThrow<Lever>(behavior);
    return name switch {
        SetStateActionName => args => OpenAction(args, component),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
    return name switch {
        SetStateActionName => SetStateActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

  #endregion

  #region Actions

  ActionDef SetStateActionDef => _setStateActionDef ??= new ActionDef {
      ScriptName = SetStateActionName,
      DisplayName = Loc.T(SetStateActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.String,
              Options = [
                  ("on", Loc.T(AutomationStateOn)),
                  ("off", Loc.T(AutomationStateOff)),
              ],
          },
      ],
  };
  ActionDef _setStateActionDef;

  static void OpenAction(ScriptValue[] args, Lever component) {
    AssertActionArgsCount(SetStateActionName, args, 1);
    switch (args[0].AsString) {
      case "on":
        component.SwitchOn();
        break;
      case "off":
        component.SwitchOff();
        break;
      default:
        // The parser should have already validated this value against the Options.
        throw new InvalidOperationException($"Invalid argument value for {SetStateActionName}: {args[0].AsString}");
    }
  }

  #endregion
}

