// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.Automation.Actions;
using IgorZ.Automation.ScriptingEngine.Expressions;

namespace IgorZ.Automation.AutomationSystem;

sealed class GameAutomationConflictDetector {
  static readonly HashSet<string> BuildingStateChangingActions = [
      "Dynamite.Detonate",
      "Dynamite.DetonateAndRepeat",
      "FillValve.Open",
      "FillValve.Close",
      "FillValve.SetHeight",
      "Floodgate.SetHeight",
      "FlowControl.Open",
      "FlowControl.Close",
      "Inventory.StartEmptying",
      "Inventory.StopEmptying",
      "Lever.SetState",
      "Manufactory.SetRecipe",
      "Pausable.Pause",
      "Pausable.Unpause",
      "Prioritizable.SetHaulers",
      "Prioritizable.ResetHaulers",
      "ThrottlingValve.Open",
      "ThrottlingValve.Close",
      "ThrottlingValve.SetFlow",
      "Workplace.RemoveWorkers",
      "Workplace.SetWorkers",
      "Workplace.SetPriority",
  ];

  public bool HasConflictingRules(AutomationBehavior behavior) {
    if (!behavior) {
      return false;
    }
    foreach (var action in behavior.Actions) {
      if (IsConflictingRule(action)) {
        return true;
      }
    }
    return false;
  }

  public bool IsBuildingStateChangingAction(ActionOperator actionOperator) {
    return actionOperator != null && BuildingStateChangingActions.Contains(actionOperator.ActionName);
  }

  public bool IsBuildingStateChangingAction(IAutomationAction action) {
    return action switch {
        ScriptedAction scriptedAction => scriptedAction.ParsingResult.ParsedExpression is ActionOperator actionOperator
            && IsBuildingStateChangingAction(actionOperator),
        _ => false,
    };
  }

  bool IsConflictingRule(IAutomationAction action) {
    if (action == null
        || action.IsMarkedForCleanup
        || action.Condition is not { IsEnabled: true, IsMarkedForCleanup: false }
        || !IsBuildingStateChangingAction(action)) {
      return false;
    }
    return true;
  }
}
