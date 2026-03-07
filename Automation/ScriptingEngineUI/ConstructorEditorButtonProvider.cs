// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
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
    var ruleConstructor = new RuleConstructor(_uiFactory);
    root.Q("RuleConstructor").Add(ruleConstructor.Root);

    PopulateConstructor(ruleRow.ActiveBuilding, ruleConstructor);
    PopulateCondition(ruleRow, ruleConstructor);
    PopulateAction(ruleRow, ruleConstructor);

    // Buttons.
    root.Q<Button>("SaveScriptBtn").clicked += () => {
      var error = ruleConstructor.ConditionConstructor.Validate() ?? ruleConstructor.ActionConstructor.Validate();
      if (error != null) {
        ruleRow.ReportError(error);
        return;
      }
      ruleRow.ConditionExpression = ToDefaultSyntax(ruleConstructor.ConditionConstructor.GetLispScript(), ruleRow);
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
    if (ruleRow.ParsedCondition is not ComparisonOperator condition) {
      return false;
    }
    if (condition.Left is not SignalOperator signal || condition.Right is not ConstantValueExpr) {
      return false;
    }
    if (!_scriptingService.GetSignalNamesForBuilding(ruleRow.ActiveBuilding).Contains(signal.SignalName)
        || !_scriptingService.GetActionNamesForBuilding(ruleRow.ActiveBuilding).Contains(action.FullActionName)) {
      return false;
    }
    if (signal.Operands.Count != 0 || action.Operands.Count > 1
        || action.Operands.Count == 1 && action.Operands[0] is not ConstantValueExpr) {
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
            Argument = new ArgumentDefinition(_uiFactory, t.Result),
        });
    ruleConstructor.ConditionConstructor.SetDefinitions(conditions);

    var actions = _scriptingService.GetActionNamesForBuilding(behavior)
        .Select(t => _scriptingService.GetActionDefinition(t, behavior))
        .Where(x => x.Arguments.Length <= 1)
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
    if (ruleRow.ParsedAction.Operands.Count > 1) {
      throw new InvalidOperationException("At most one argument is expected");
    }
    if (ruleRow.ParsedAction.Operands[0] is not ConstantValueExpr constantValue) {
      throw new InvalidOperationException("Constant value is expected");
    }
    actionConstructor.ArgumentConstructor.SetScriptValue(constantValue.ValueFn());
  }

  static void PopulateCondition(RuleRow ruleRow, RuleConstructor ruleConstructor) {
    if (ruleRow.ParsedCondition == null) {
      return;
    }
    var conditionConstructor = ruleConstructor.ConditionConstructor;
    if (ruleRow.ParsedCondition is not ComparisonOperator comparisonOperator) {
      throw new InvalidOperationException("Binary operator is expected, but found: " + ruleRow.ParsedCondition);
    }
    conditionConstructor.SignalSelector.SelectedValue = (comparisonOperator.Left as SignalOperator)!.SignalName;
    conditionConstructor.OperatorSelector.SelectedValue =
        LispSyntaxParser.ComparisonOperators[comparisonOperator.OperatorType];
    if (comparisonOperator.Right is not ConstantValueExpr constantValue) {
      throw new InvalidOperationException("Constant value is expected");
    }
    conditionConstructor.ValueSelector.SetScriptValue(constantValue.ValueFn());
  }

  #endregion
}
