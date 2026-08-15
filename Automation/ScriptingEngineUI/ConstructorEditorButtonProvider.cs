// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.Settings;
using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ConstructorEditorButtonProvider : IEditorButtonProvider {

  const string AddRuleViaConstructorBtnLocKey = "IgorZ.Automation.Scripting.Editor.AddRuleViaConstructorBtn";
  const string EditInConstructorBtnLocKey = "IgorZ.Automation.Scripting.Editor.EditInConstructorBtn";

  #region IEditorProvider implementation

  /// <inheritdoc/>
  public string CreateRuleBtnLocKey => AddRuleViaConstructorBtnLocKey;

  /// <inheritdoc/>
  public string RuleRowBtnLocKey => EditInConstructorBtnLocKey;

  /// <inheritdoc/>
  public void OnRuleRowBtnAction(RuleRow ruleRow) {
    var root = _uiFactory.LoadVisualElement("IgorZ.Automation/ConstructorEditView");
    var ruleConstructor = new RuleConstructor(_uiFactory, root.Q2<VisualElement>("RuleConstructor"));

    PopulateConstructor(ruleRow.ActiveBuilding, ruleConstructor);
    PopulateCondition(ruleRow, ruleConstructor);
    PopulateAction(ruleRow, ruleConstructor);

    // Buttons.
    root.Q<Button>("SaveScriptBtn").clicked += () => {
      var error = ruleConstructor.Validate();
      if (error != null) {
        ruleRow.ReportError(error);
        return;
      }
      ruleRow.ConditionExpression = ToDefaultSyntax(ruleConstructor.GetConditionLispScript(), ruleRow);
      ruleRow.ActionExpression = ToDefaultSyntax(ruleConstructor.ActionConstructor.GetLispScript(), ruleRow);
      ruleRow.SwitchToViewMode();
    };
    root.Q<Button>("DiscardScriptBtn").clicked += ruleRow.DiscardChangesAndSwitchToViewMode;

    ruleRow.CreateEditView(root);
  }

  /// <inheritdoc/>
  public bool IsRuleRowBtnEnabled(RuleRow ruleRow) {
    var action = ruleRow.ParsedAction;
    if (ruleRow.ParsedCondition == null || action == null) {
      return false;
    }
    if (!TryGetConditionEntries(ruleRow.ParsedCondition, out var conditions)) {
      return false;
    }
    if (conditions.Any(x => x.Comparison.Left is not SignalOperator signal
            || x.Comparison.Right is not ConstantValueExpr
            || !_scriptingService.GetSignalNamesForBuilding(ruleRow.ActiveBuilding).Contains(signal.SignalName))
        || !_scriptingService.GetActionNamesForBuilding(ruleRow.ActiveBuilding).Contains(action.FullActionName)) {
      return false;
    }
    if (conditions.Any(x => ((SignalOperator)x.Comparison.Left).Operands.Count != 0)
        || action.Operands.Any(x => x is not ConstantValueExpr)) {
      return false;
    }
    return true;
  }

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  readonly ScriptingService _scriptingService;
  readonly ParserFactory _parserFactory;
  
  ConstructorEditorButtonProvider(UiFactory uiFactory, ScriptingService scriptingService, ParserFactory parserFactory) {
    _uiFactory = uiFactory;
    _scriptingService = scriptingService;
    _parserFactory = parserFactory;
  }

  void PopulateConstructor(AutomationBehavior behavior, RuleConstructor ruleConstructor) {
    var conditions = _scriptingService.GetSignalNamesForBuilding(behavior)
        .Select(t => _scriptingService.GetSignalDefinition(t, behavior))
        .Select(t => new ConditionConstructor.ConditionDefinition {
            Name = (t.ScriptName, t.DisplayName),
            DisplayNameLocKey = t.DisplayNameLocKey,
            DisplayNameArgument = t.DisplayNameArgument,
            Argument = new ArgumentDefinition(_uiFactory, t.Result),
        });
    ruleConstructor.SetConditionDefinitions(conditions);

    var actions = _scriptingService.GetActionNamesForBuilding(behavior)
        .Select(t => _scriptingService.GetActionDefinition(t, behavior))
        .Select(t => new ActionConstructor.ActionDefinition {
            Name = (t.ScriptName, t.DisplayName),
            Arguments = t.Arguments.Select(v => new ArgumentDefinition(_uiFactory, v)).ToArray(),
        });
    ruleConstructor.ActionConstructor.SetDefinitions(actions);
  }

  string ToDefaultSyntax(string lispSyntax, RuleRow ruleRow) {
    if (ScriptEditorSettings.DefaultScriptSyntax == ScriptEditorSettings.ScriptSyntax.Lisp) {
      return lispSyntax;
    }
    var result = _parserFactory.LispSyntaxParser.Parse(lispSyntax, ruleRow.ActiveBuilding);
    if (result.LastScriptError != null) {
      //FXIME: maybe deal with it better?
      throw result.LastScriptError;  // Not expected!
    }
    return _parserFactory.DefaultParser.Decompile(result.ParsedExpression);
  }

  static void PopulateAction(RuleRow ruleRow, RuleConstructor ruleConstructor) {
    if (ruleRow.ParsedCondition == null) {
      return;
    }
    var actionConstructor = ruleConstructor.ActionConstructor;
    actionConstructor.ActionSelector.SelectedValue = ruleRow.ParsedAction.FullActionName;
    if (ruleRow.ParsedAction.Operands.Count == 0) {
      return;
    }
    var argumentValues = new List<ScriptValue>();
    foreach (var argument in ruleRow.ParsedAction.Operands) {
      if (argument is not ConstantValueExpr constantValueExpr) {
        throw new InvalidOperationException("Constant value is expected");
      }
      argumentValues.Add(constantValueExpr.ValueFn());
    }
    actionConstructor.SetArguments(argumentValues);
  }

  static void PopulateCondition(RuleRow ruleRow, RuleConstructor ruleConstructor) {
    if (ruleRow.ParsedCondition == null) {
      return;
    }
    if (!TryGetConditionEntries(ruleRow.ParsedCondition, out var conditions)) {
      throw new InvalidOperationException("A flat condition chain is expected, but found: " + ruleRow.ParsedCondition);
    }
    ruleConstructor.SetConditions(conditions);
  }

  static bool TryGetConditionEntries(
      BooleanOperator expression, out IReadOnlyList<RuleConstructor.ConditionEntry> conditions) {
    var result = new List<RuleConstructor.ConditionEntry>();
    if (expression is ComparisonOperator comparison) {
      result.Add(new RuleConstructor.ConditionEntry(LogicalOperator.OpType.And, comparison));
      conditions = result;
      return true;
    }
    if (expression is not LogicalOperator logical || logical.OperatorType == LogicalOperator.OpType.Not) {
      conditions = null;
      return false;
    }

    var topLevelOperands = logical.OperatorType == LogicalOperator.OpType.Or
        ? FlattenLogicalOperands(logical, LogicalOperator.OpType.Or)
        : [logical];
    foreach (var topLevelOperand in topLevelOperands) {
      var groupOperands = topLevelOperand is LogicalOperator { OperatorType: LogicalOperator.OpType.And } andGroup
          ? FlattenLogicalOperands(andGroup, LogicalOperator.OpType.And)
          : [topLevelOperand];
      var firstInGroup = true;
      foreach (var operand in groupOperands) {
        if (operand is not ComparisonOperator groupComparison) {
          conditions = null;
          return false;
        }
        var joinOperator = result.Count == 0 || !firstInGroup
            ? LogicalOperator.OpType.And
            : LogicalOperator.OpType.Or;
        result.Add(new RuleConstructor.ConditionEntry(joinOperator, groupComparison));
        firstInGroup = false;
      }
    }
    conditions = result;
    return result.Count > 0;
  }

  static IReadOnlyList<IExpression> FlattenLogicalOperands(
      LogicalOperator expression, LogicalOperator.OpType operatorType) {
    var result = new List<IExpression>();
    foreach (var operand in expression.Operands) {
      if (operand is LogicalOperator nested && nested.OperatorType == operatorType) {
        result.AddRange(FlattenLogicalOperands(nested, operatorType));
      } else {
        result.Add(operand);
      }
    }
    return result;
  }

  #endregion
}
