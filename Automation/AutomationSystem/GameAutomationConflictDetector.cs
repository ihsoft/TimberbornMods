// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.WaterBuildings;
using Timberborn.WaterSourceSystem;

namespace IgorZ.Automation.AutomationSystem;

sealed class GameAutomationConflictDetector {
  const string DynamiteDetonationProperty = "Dynamite.Detonation";
  const string FillValveTargetHeightProperty = "FillValve.TargetHeight";
  const string FillValveAutomationTargetHeightProperty = "FillValve.AutomationTargetHeight";
  const string FloodgateHeightProperty = "Floodgate.Height";
  const string FloodgateAutomationHeightProperty = "Floodgate.AutomationHeight";
  const string FlowControlStateProperty = "FlowControl.State";
  const string InventoryEmptyingProperty = "Inventory.Emptying";
  const string LeverStateProperty = "Lever.State";
  const string ManufactoryRecipeProperty = "Manufactory.Recipe";
  const string PausableBuildingPausedProperty = "PausableBuilding.Paused";
  const string PrioritizableHaulersProperty = "Prioritizable.Haulers";
  const string ThrottlingValveOutflowLimitProperty = "ThrottlingValve.OutflowLimit";
  const string ThrottlingValveAutomationOutflowLimitProperty = "ThrottlingValve.AutomationOutflowLimit";
  const string WorkplaceWorkersProperty = "Workplace.Workers";
  const string WorkplacePriorityProperty = "Workplace.Priority";

  static readonly Dictionary<string, string[]> ActionChangedProperties = new() {
      ["Dynamite.Detonate"] = [DynamiteDetonationProperty],
      ["Dynamite.DetonateAndRepeat"] = [DynamiteDetonationProperty],
      ["FillValve.Open"] = [FillValveTargetHeightProperty],
      ["FillValve.Close"] = [FillValveTargetHeightProperty],
      ["FillValve.SetHeight"] = [FillValveTargetHeightProperty],
      ["Floodgate.SetHeight"] = [FloodgateHeightProperty],
      ["FlowControl.Open"] = [FlowControlStateProperty],
      ["FlowControl.Close"] = [FlowControlStateProperty],
      ["Inventory.StartEmptying"] = [InventoryEmptyingProperty],
      ["Inventory.StopEmptying"] = [InventoryEmptyingProperty],
      ["Lever.SetState"] = [LeverStateProperty],
      ["Manufactory.SetRecipe"] = [ManufactoryRecipeProperty],
      ["Pausable.Pause"] = [PausableBuildingPausedProperty],
      ["Pausable.Unpause"] = [PausableBuildingPausedProperty],
      ["Prioritizable.SetHaulers"] = [PrioritizableHaulersProperty],
      ["Prioritizable.ResetHaulers"] = [PrioritizableHaulersProperty],
      ["ThrottlingValve.Open"] = [ThrottlingValveOutflowLimitProperty],
      ["ThrottlingValve.Close"] = [ThrottlingValveOutflowLimitProperty],
      ["ThrottlingValve.SetFlow"] = [ThrottlingValveOutflowLimitProperty],
      ["Workplace.RemoveWorkers"] = [WorkplaceWorkersProperty],
      ["Workplace.SetWorkers"] = [WorkplaceWorkersProperty],
      ["Workplace.SetPriority"] = [WorkplacePriorityProperty],
  };

  public bool HasConflictingRules(AutomationBehavior behavior) {
    var gameAutomationProperties = GetGameAutomationChangedProperties(behavior);
    foreach (var action in behavior.Actions) {
      if (IsConflictingRule(action, gameAutomationProperties)) {
        return true;
      }
    }
    return false;
  }

  public bool IsBuildingStateChangingAction(ActionOperator actionOperator) {
    return actionOperator != null && ActionChangedProperties.ContainsKey(actionOperator.ActionName);
  }

  public bool IsBuildingStateChangingAction(IAutomationAction action) {
    return action switch {
        ScriptedAction scriptedAction => scriptedAction.ParsingResult.ParsedExpression is ActionOperator actionOperator
            && IsBuildingStateChangingAction(actionOperator),
        _ => false,
    };
  }

  public bool IsConflictingRule(AutomationBehavior behavior, ActionOperator actionOperator) {
    return IsConflictingAction(actionOperator, GetGameAutomationChangedProperties(behavior));
  }

  bool IsConflictingRule(IAutomationAction action, HashSet<string> gameAutomationProperties) {
    if (action == null
        || action.IsMarkedForCleanup
        || action.Condition is not { IsEnabled: true, IsMarkedForCleanup: false }
        || action is not ScriptedAction scriptedAction
        || scriptedAction.ParsingResult.ParsedExpression is not ActionOperator actionOperator
        || !IsConflictingAction(actionOperator, gameAutomationProperties)) {
      return false;
    }
    return true;
  }

  bool IsConflictingAction(ActionOperator actionOperator, HashSet<string> gameAutomationProperties) {
    return actionOperator != null
        && ActionChangedProperties.TryGetValue(actionOperator.ActionName, out var actionProperties)
        && actionProperties.Any(gameAutomationProperties.Contains);
  }

  static HashSet<string> GetGameAutomationChangedProperties(AutomationBehavior behavior) {
    var properties = new HashSet<string>();
    if (behavior.GetComponent<WaterSourceRegulator>()) {
      properties.Add(FlowControlStateProperty);
    }
    if (behavior.GetComponent<FillValve>()) {
      properties.Add(FillValveAutomationTargetHeightProperty);
    }
    if (behavior.GetComponent<Floodgate>()) {
      properties.Add(FloodgateAutomationHeightProperty);
    }
    if (behavior.GetComponent<ThrottlingValve>()) {
      properties.Add(ThrottlingValveAutomationOutflowLimitProperty);
    }
    return properties;
  }
}
