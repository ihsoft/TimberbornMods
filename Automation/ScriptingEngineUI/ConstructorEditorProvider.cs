// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ConstructorEditorProvider : IEditorProvider {

  const string AddRuleViaConstructorBtnLocKey = "IgorZ.Automation.Scripting.Editor.AddRuleViaConstructorBtn";
  const string EditInConstructorBtnLocKey = "IgorZ.Automation.Scripting.Editor.EditInConstructorBtn";

  const string StringConstantTypeLocKey = "IgorZ.Automation.Scripting.Editor.StringConstantType";
  const string NumberConstantTypeLocKey = "IgorZ.Automation.Scripting.Editor.NumberConstantType";

  #region IEditorProvider implementation

  /// <inheritdoc/>
  public string CreateRuleLocKey => AddRuleViaConstructorBtnLocKey;

  /// <inheritdoc/>
  public string EditRuleLocKey => EditInConstructorBtnLocKey;

  /// <inheritdoc/>
  public void MakeForRule(RuleRow ruleRow) {
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
      ruleRow.ConditionExpression = ruleConstructor.ConditionConstructor.GetScript();
      ruleRow.ActionExpression = ruleConstructor.ActionConstructor.GetScript();
      ruleRow.SwitchToViewMode();
    };
    root.Q<Button>("DiscardScriptBtn").clicked += ruleRow.DiscardChangesAndSwitchToViewMode;

    ruleRow.CreateEditView(root);
  }

  /// <inheritdoc/>
  public bool VerifyIfEditable(RuleRow ruleRow) {
    var action = ruleRow.ParsedAction;
    if (ruleRow.ParsedCondition == null || action == null) {
      return false;
    }
    if (ruleRow.ParsedCondition is not BinaryOperator condition) {
      return false;
    }
    if (condition.Left is not SignalOperator signal || condition.Right is not ConstantValueExpr) {
      return false;
    }
    if (!_scriptingService.GetSignalNamesForBuilding(ruleRow.ActiveBuilding).Contains(signal.FullSignalName)
        || !_scriptingService.GetActionNamesForBuilding(ruleRow.ActiveBuilding).Contains(action.FullActionName)) {
      return false;
    }
    if (signal.Operands.Count != 1 || action.Operands.Count > 2
        || action.Operands.Count == 2 && action.Operands[1] is not ConstantValueExpr) {
      return false;
    }
    return true;
  }

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  readonly ScriptingService _scriptingService;
  
  ConstructorEditorProvider(UiFactory uiFactory, ScriptingService scriptingService) {
    _uiFactory = uiFactory;
    _scriptingService = scriptingService;
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

  static void PopulateAction(RuleRow ruleRow, RuleConstructor ruleConstructor) {
    if (ruleRow.ParsedCondition == null) {
      return;
    }
    var actionConstructor = ruleConstructor.ActionConstructor;
    actionConstructor.ActionSelector.SelectedValue = ruleRow.ParsedAction.FullActionName;
    if (ruleRow.ParsedAction.Operands.Count == 1) {
      return;
    }
    if (ruleRow.ParsedAction.Operands.Count > 2) {
      throw new InvalidOperationException("At most one argument is expected");
    }
    if (ruleRow.ParsedAction.Operands[1] is not ConstantValueExpr constantValue) {
      throw new InvalidOperationException("Constant value is expected");
    }
    actionConstructor.ArgumentConstructor.Value = PrepareConstantValue(constantValue.ValueFn());
  }

  static void PopulateCondition(RuleRow ruleRow, RuleConstructor ruleConstructor) {
    if (ruleRow.ParsedCondition == null) {
      return;
    }
    var conditionConstructor = ruleConstructor.ConditionConstructor;
    if (ruleRow.ParsedCondition is not BinaryOperator binaryOperatorExpr) {
      throw new InvalidOperationException("Binary operator is expected, but found: " + ruleRow.ParsedCondition);
    }
    conditionConstructor.SignalSelector.SelectedValue = (binaryOperatorExpr.Left as SignalOperator)!.FullSignalName;
    conditionConstructor.OperatorSelector.SelectedValue = binaryOperatorExpr.Name;
    if (binaryOperatorExpr.Right is not ConstantValueExpr constantValue) {
      throw new InvalidOperationException("Constant value is expected");
    }
    conditionConstructor.ValueSelector.Value = PrepareConstantValue(constantValue.ValueFn());
  }

  static string PrepareConstantValue(ScriptValue value) {
    return value.ValueType == ScriptValue.TypeEnum.Number
        ? (value.AsNumber / 100f).ToString("0.##")
        : value.AsString;
  }

  #endregion
}
