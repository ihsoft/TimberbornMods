// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.Automation.Actions;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.AutomationSystem;

sealed class GameAutomationConflictDetector {
  static readonly HashSet<string> FillValveStateChangingActions = [
      "FillValve.Open",
      "FillValve.Close",
      "FillValve.SetHeight",
  ];

  static readonly HashSet<string> ThrottlingValveStateChangingActions = [
      "ThrottlingValve.Open",
      "ThrottlingValve.Close",
      "ThrottlingValve.SetFlow",
  ];

  public bool HasConflictingRules(AutomationBehavior behavior) {
    if (!behavior) {
      return false;
    }
    foreach (var action in behavior.Actions) {
      if (IsConflictingRule(action, behavior)) {
        return true;
      }
    }
    return false;
  }

  static bool IsConflictingRule(IAutomationAction action, AutomationBehavior behavior) {
    if (action is not ScriptedAction scriptedAction
        || action.IsMarkedForCleanup
        || action.Condition is not { IsEnabled: true, IsMarkedForCleanup: false }
        || scriptedAction.ParsingResult.ParsedExpression is not ActionOperator actionOperator) {
      return false;
    }
    return IsStateChangingActionForBuilding(actionOperator.ActionName, behavior);
  }

  static bool IsStateChangingActionForBuilding(string actionName, AutomationBehavior behavior) {
    return (behavior.GetComponent<FillValve>() && FillValveStateChangingActions.Contains(actionName))
        || (behavior.GetComponent<ThrottlingValve>() && ThrottlingValveStateChangingActions.Contains(actionName));
  }
}
