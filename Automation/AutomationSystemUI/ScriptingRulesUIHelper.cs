using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;

namespace IgorZ.Automation.AutomationSystemUI;

public class ScriptingRulesUIHelper {
  // FIXME: Addd "District.". Or not.
  // FIXME: add scope to the signal definition and filter by that
  static readonly List<string> NonBuildingActions = [
      "Debug.", "Weather.", "Signals.", "District.",
  ];

  public record StructureSignalPresenter {
    public string Describe { get; init; }
    public string SignalName { get; init; }
    public string ExportedSignalName { get; init; }
  }


  public static List<StructureSignal> ExtractStructureSignal(AutomationBehavior automationBehavior) {
  }

  public Dictionary<string, string> MakeSignalsDictionary(AutomationBehavior automationBehavior) {
    var dict = new Dictionary<string, string>();
    var signals = _scriptingService.GetSignalNamesForBuilding(automationBehavior)
        .Where(x => !NonBuildingActions.Any(x.StartsWith));
    foreach (var signal in signals) {
      dict[signal] = null;
    }
    var scriptedRules = automationBehavior.Actions
        .Where(x => x.Condition is ScriptedCondition && x is ScriptedAction)
        .Cast<ScriptedAction>();
    foreach (var rule in scriptedRules) {
      var mapping = TryGetSignalMapping((ScriptedCondition)rule.Condition, rule, automationBehavior);
      if (mapping.buildingSignal != null) {
        dict[mapping.buildingSignal] = mapping.customSignal;
      }
    }
    return dict;
  }

  public bool IsSignalMapping(IAutomationAction action) {
    if (action.Condition is not ScriptedCondition scriptedCondition || action is not ScriptedAction scriptedAction) {
      return false;
    }
    var mapping = TryGetSignalMapping2(scriptedCondition.ParsedExpression, scriptedAction.ParsedExpression);
    return mapping.buildingSignal != null;
  }

  // Detects special condition/action setup to "export" building's signal as a custom signal:
  // If (eq (sig ABC) (sig ABC)), then (act Signals.Set "CustomSignal" (sig ABC))
  // Means mapping: ABC -> Signals.CustomSignal
  (string buildingSignal, string customSignal) TryGetSignalMapping(
      ScriptedCondition condition, ScriptedAction action, AutomationBehavior automationBehavior) {
    var conditionParseResult = _expressionParser.Parse(condition.Expression, automationBehavior);
    var actionParseResult = _expressionParser.Parse(action.Expression, automationBehavior);
    if (conditionParseResult.ParsedExpression == null || actionParseResult.ParsedExpression == null) {
      return (null, null);
    }
    if (actionParseResult.ParsedExpression is not ActionOperator { ActionName: "Signals.Set" } actionOperator
        || actionOperator.Operands[1] is not ConstantValueExpr { ValueType: ScriptValue.TypeEnum.String } actionExpr
        || actionOperator.Operands[2] is not SignalOperator actionSignalOperator) {
      return (null, null);
    }
    if (conditionParseResult.ParsedExpression is not BinaryOperator { Name: "eq" } binaryOperator
        || binaryOperator.Left is not SignalOperator leftSignalOperator
        || binaryOperator.Right is not SignalOperator rightSignalOperator
        || leftSignalOperator.SignalName != rightSignalOperator.SignalName
        || leftSignalOperator.SignalName != actionSignalOperator.SignalName) {
      return (null, null);
    }
    return (actionSignalOperator.SignalName, actionExpr.ValueFn().AsString);
  }

  (string buildingSignal, string customSignal) TryGetSignalMapping2(BoolOperator condition, ActionOperator action) {
    if (action is not { ActionName: "Signals.Set" } actionOperator
        || actionOperator.Operands[1] is not ConstantValueExpr { ValueType: ScriptValue.TypeEnum.String } actionExpr
        || actionOperator.Operands[2] is not SignalOperator actionSignalOperator) {
      return (null, null);
    }
    if (condition is not BinaryOperator { Name: "eq" } binaryOperator
        || binaryOperator.Left is not SignalOperator leftSignalOperator
        || binaryOperator.Right is not SignalOperator rightSignalOperator
        || leftSignalOperator.SignalName != rightSignalOperator.SignalName
        || leftSignalOperator.SignalName != actionSignalOperator.SignalName) {
      return (null, null);
    }
    return (actionSignalOperator.SignalName, actionExpr.ValueFn().AsString);
  }

  readonly ScriptingService _scriptingService;
  readonly ExpressionParser _expressionParser;

  ScriptingRulesUIHelper(ScriptingService scriptingService, ExpressionParser expressionParser) {
    _scriptingService = scriptingService;
    _expressionParser = expressionParser;
  }
}
