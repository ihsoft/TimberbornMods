// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ScriptEditorProvider : IEditorProvider {

  const string ConditionMustBeBoolLocKey = "IgorZ.Automation.Scripting.Editor.ConditionMustBeBoolean";
  const string ActionMustBeActionLocKey = "IgorZ.Automation.Scripting.Editor.ActionMustBeAction";
  const string ConditionMustHaveSignalsLocKey = "IgorZ.Automation.Scripting.Editor.ConditionMustHaveSignals";

  const string AddRuleFromScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.AddRuleFromScriptBtn";
  const string EditAsScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.EditAsScriptBtn";

  const int TestScriptStatusHighlightDurationMs = 1000;
  const string GoodScriptClass = "text-field-green";
  const string BadScriptClass = "text-field-error";

  #region IEditorProvider implementation

  /// <inheritdoc/>
  public string CreateRuleLocKey => AddRuleFromScriptBtnLocKey;

  /// <inheritdoc/>
  public string EditRuleLocKey => EditAsScriptBtnLocKey;

  /// <inheritdoc/>
  public void MakeForRule(RuleRow ruleRow) {
    var root = _uiFactory.LoadVisualElement("IgorZ.Automation/ScriptEditView");
    var conditionEdit = root.Q<TextField>("ConditionScript");
    conditionEdit.SetValueWithoutNotify(ruleRow.ConditionExpression ?? "");
    var actionEdit = root.Q<TextField>("ActionScript");
    actionEdit.SetValueWithoutNotify(ruleRow.ActionExpression ?? "");

    // Buttons.
    root.Q<Button>("SaveScriptBtn").clicked += () => {
      if (!RunRuleCheck(ruleRow, conditionEdit, actionEdit)) {
        return;
      }
      ruleRow.ConditionExpression = conditionEdit.value;
      ruleRow.ActionExpression = actionEdit.value;
      ruleRow.SwitchToViewMode();
    };
    root.Q<Button>("DiscardScriptBtn").clicked += ruleRow.DiscardChangesAndSwitchToViewMode;
    root.Q<Button>("TestScriptBtn").clicked += () => {
      if (!RunRuleCheck(ruleRow, conditionEdit, actionEdit)) {
        return;
      }
      VisualEffects.SetTemporaryClass(conditionEdit, TestScriptStatusHighlightDurationMs, GoodScriptClass);
      VisualEffects.SetTemporaryClass(actionEdit, TestScriptStatusHighlightDurationMs, GoodScriptClass);
    };

    ruleRow.CreateEditView(root);
  }

  /// <inheritdoc/>
  public bool VerifyIfEditable(RuleRow ruleRow) {
    return ruleRow.LegacyAction == null;
  }

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  readonly ExpressionParser _expressionParser;
  
  ScriptEditorProvider(UiFactory uiFactory, ExpressionParser expressionParser) {
    _uiFactory = uiFactory;
    _expressionParser = expressionParser;
  }

  bool RunRuleCheck(RuleRow ruleRow, TextField conditionEdit, TextField actionEdit) {
    ruleRow.ClearError();
    return CheckExpressionAndShowError(ruleRow, conditionEdit, true)
        && CheckExpressionAndShowError(ruleRow, actionEdit, false);
  }

  bool CheckExpressionAndShowError(RuleRow ruleRow, TextField expressionField, bool isCondition) {
    var result = _expressionParser.Parse(expressionField.value, ruleRow.ActiveBuilding);
    if (result.LastScriptError != null) {
      ruleRow.ReportError(result.LastScriptError);
      VisualEffects.SetTemporaryClass(expressionField, TestScriptStatusHighlightDurationMs, BadScriptClass);
      return false;
    }
    string error = null;
    if (isCondition) {
      if (result.ParsedExpression is not BoolOperator) {
        error = _uiFactory.T(ConditionMustBeBoolLocKey);
      } else {
        var hasSignals = false;
        result.ParsedExpression.VisitNodes(x => { hasSignals |= x is SignalOperator; });
        if (!hasSignals) {
          error = _uiFactory.T(ConditionMustHaveSignalsLocKey);
        }
      }
    } else {
      if (result.ParsedExpression is not ActionOperator) {
        error = _uiFactory.T(ActionMustBeActionLocKey);
      }
    }
    if (error == null) {
      return true;
    }
    ruleRow.ReportError(error);
    VisualEffects.SetTemporaryClass(expressionField, TestScriptStatusHighlightDurationMs, BadScriptClass);
    return false;
  }

  #endregion
}
