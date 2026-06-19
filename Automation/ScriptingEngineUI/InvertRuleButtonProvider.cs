// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.WaterBuildings;
using Timberborn.WorkSystem;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class InvertRuleButtonProvider(ParserFactory parserFactory) : IEditorButtonProvider {

  const string InvertRuleBtnLocKey = "IgorZ.Automation.Scripting.Editor.InvertRuleBtn";

  /// <inheritdoc/>
  public string CreateRuleBtnLocKey => null;

  /// <inheritdoc/>
  public string RuleRowBtnLocKey => InvertRuleBtnLocKey;

  /// <inheritdoc/>
  public void OnRuleRowBtnAction(RuleRow ruleRow) {
    var invertedConditionExpression = MakeInvertedConditionExpression(ruleRow.ParsedCondition);
    if (invertedConditionExpression != null) {
      var result = parserFactory.LispSyntaxParser.Parse(invertedConditionExpression, ruleRow.ActiveBuilding);
      if (result.LastScriptError != null) {
        throw new InvalidOperationException(
            $"Cannot parse inverted condition: {invertedConditionExpression} => {result.LastError}");
      }
      ruleRow.ConditionExpression = parserFactory.DefaultParser.Decompile(result.ParsedExpression);
    } else {
      var inverted = parserFactory.DefaultParser.InvertBooleanExpression(ruleRow.ParsedCondition, ruleRow.ActiveBuilding);
      ruleRow.ConditionExpression = parserFactory.DefaultParser.Decompile(inverted);
    }
    var invertedActionExpression = MakeInvertedActionExpression(ruleRow.ParsedAction, ruleRow.ActiveBuilding);
    if (invertedActionExpression != null) {
      var result = parserFactory.LispSyntaxParser.Parse(invertedActionExpression, ruleRow.ActiveBuilding);
      if (result.LastScriptError != null) {
        throw new InvalidOperationException(
            $"Cannot parse inverted action: {invertedActionExpression} => {result.LastError}");
      }
      ruleRow.ActionExpression = parserFactory.DefaultParser.Decompile(result.ParsedExpression); 
    }
    ruleRow.SwitchToViewMode();
  }

  /// <inheritdoc/>
  public bool IsRuleRowBtnEnabled(RuleRow ruleRow) {
    return ruleRow.ParsedCondition != null && ruleRow.ParsedAction != null;
  }

  string MakeInvertedConditionExpression(BooleanOperator condition) {
    if (condition is not ComparisonOperator comparison) {
      return null;
    }
    if (comparison.OperatorType is not (ComparisonOperator.OpType.Equal or ComparisonOperator.OpType.NotEqual)) {
      return null;
    }
    if (!TryGetTimeWorkingHoursComparison(comparison, out var currentValue)) {
      return null;
    }
    var invertedValue = currentValue switch {
        "Working" => "OffHours",
        "OffHours" => "Working",
        _ => null,
    };
    if (invertedValue == null) {
      return null;
    }
    var operatorName = comparison.OperatorType == ComparisonOperator.OpType.Equal ? "eq" : "ne";
    return $"({operatorName} (sig Time.WorkingHours) '{invertedValue}')";
  }

  string MakeInvertedActionExpression(ActionOperator action, AutomationBehavior automationBehavior) {
    var actioName = action.ActionName;
    if (actioName == "Floodgate.SetHeight") {
      var floodgate = automationBehavior.GetComponent<Floodgate>();
      var argHeight = ((IValueExpr)action.Operands[0]).ValueFn().AsInt;
      if (argHeight == 0) {
        return $"(act Floodgate.SetHeight {floodgate.MaxHeight * 100})";
      }
      if (argHeight == floodgate.MaxHeight) {
        return "(act Floodgate.SetHeight 0)";
      }
      return null;
    }
    if (actioName == "FillValve.SetHeight") {
      var fillValve = automationBehavior.GetComponent<FillValve>();
      var argHeight = ((IValueExpr)action.Operands[0]).ValueFn().AsInt;
      var maxTargetDepth = fillValve.MaxTargetHeight - fillValve.MinTargetHeight;
      if (argHeight == 0) {
        return $"(act FillValve.SetHeight {maxTargetDepth * 100})";
      }
      if (argHeight == maxTargetDepth) {
        return "(act FillValve.SetHeight 0)";
      }
      return null;
    }
    if (actioName == "ThrottlingValve.SetFlow") {
      var valve = automationBehavior.GetComponent<ThrottlingValve>();
      var argFlow = ((IValueExpr)action.Operands[0]).ValueFn().AsFloat;
      if (argFlow == 0) {
        return $"(act ThrottlingValve.SetFlow {valve.MaxOutflowLimit * 100})";
      }
      if (argFlow == valve.MaxOutflowLimit) {
        return "(act ThrottlingValve.SetFlow 0)";
      }
      return null;
    }
    if (actioName is "Workplace.SetWorkers" or "Workplace.RemoveWorkers") {
      var workplace = automationBehavior.GetComponent<Workplace>();
      var argNumWorkers = actioName == "Workplace.SetWorkers" ? ((IValueExpr)action.Operands[0]).ValueFn().AsInt : 0;
      if (argNumWorkers == 0) {
        return $"(act Workplace.SetWorkers {workplace.MaxWorkers * 100})";
      }
      if (argNumWorkers == workplace.MaxWorkers) {
        return "(act Workplace.RemoveWorkers)";
      }
      return null;
    }
    return action.ActionName switch {
        "FillValve.Open" => "(act FillValve.Close)",
        "FillValve.Close" => "(act FillValve.Open)",
        "ThrottlingValve.Open" => "(act ThrottlingValve.Close)",
        "ThrottlingValve.Close" => "(act ThrottlingValve.Open)",
        "Clutch.Engage" => "(act Clutch.Disengage)",
        "Clutch.Disengage" => "(act Clutch.Engage)",
        "FlowControl.Open" => "(act FlowControl.Close)",
        "FlowControl.Close" => "(act FlowControl.Open)",
        "Inventory.StartEmptying" => "(act Inventory.StopEmptying)",
        "Inventory.StopEmptying" => "(act Inventory.StartEmptying)",
        "Pausable.Pause" => "(act Pausable.Unpause)",
        "Pausable.Unpause" => "(act Pausable.Pause)",
        "Prioritizable.SetHaulers" => "(act Prioritizable.ResetHaulers)",
        "Prioritizable.ResetHaulers" => "(act Prioritizable.SetHaulers)",
        _ => null,
    };
  }

  static bool TryGetTimeWorkingHoursComparison(ComparisonOperator comparison, out string value) {
    value = null;
    if (comparison.Left is SignalOperator { SignalName: "Time.WorkingHours" }
        && comparison.Right is ConstantValueExpr { ValueType: ScriptValue.TypeEnum.String } rightValue) {
      value = rightValue.ValueFn().AsString;
      return true;
    }
    if (comparison.Right is SignalOperator { SignalName: "Time.WorkingHours" }
        && comparison.Left is ConstantValueExpr { ValueType: ScriptValue.TypeEnum.String } leftValue) {
      value = leftValue.ValueFn().AsString;
      return true;
    }
    return false;
  }
}
