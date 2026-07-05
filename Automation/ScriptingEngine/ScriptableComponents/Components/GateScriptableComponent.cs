// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.AutomationBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class GateScriptableComponent : ScriptableComponentBase {

  const string SetStateActionLocKey = "IgorZ.Automation.Scriptable.Gate.Action.SetState";

  const string GateStateOpen = "Toggle.State.Open";
  const string GateStateClosed = "Toggle.State.Closed";
  const string GateStateAutomated = "Automation.Mode.Automated";

  const string SetStateActionName = "Gate.SetState";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Gate";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<Gate>() ? [SetStateActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var gate = GetComponentOrThrow<Gate>(behavior);
    return name switch {
        SetStateActionName => args => SetStateAction(gate, args),
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
                  ("open", Loc.T(GateStateOpen)),
                  ("closed", Loc.T(GateStateClosed)),
                  ("automated", Loc.T(GateStateAutomated)),
              ],
          },
      ],
  };
  ActionDef _setStateActionDef;

  static void SetStateAction(Gate gate, ScriptValue[] args) {
    AssertActionArgsCount(SetStateActionName, args, 1);
    switch (args[0].AsString) {
      case "open":
        gate.Open();
        break;
      case "closed":
        gate.Close();
        break;
      case "automated":
        gate.Automate();
        break;
      default:
        // The parser should have already validated this value against the Options.
        throw new InvalidOperationException($"Invalid argument value for {SetStateActionName}: {args[0].AsString}");
    }
  }

  #endregion
}
