// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ScriptEditorProvider : IEditorProvider {

  const string ConditionMustBeBoolLocKey = "IgorZ.Automation.Scripting.Editor.ConditionMustBeBoolean";
  const string ActionMustBeActionLocKey = "IgorZ.Automation.Scripting.Editor.ActionMustBeAction";

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
  public bool VerifyIfEditable(RuleRow ruleRow, AutomationBehavior _) {
    return ruleRow.LegacyAction == null;
  }

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  
  ScriptEditorProvider(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  bool RunRuleCheck(RuleRow ruleRow, TextField conditionEdit, TextField actionEdit) {
    ruleRow.ClearError();
    return CheckExpressionAndShowError(ruleRow, conditionEdit, true)
        && CheckExpressionAndShowError(ruleRow, actionEdit, false);
  }

  bool CheckExpressionAndShowError(RuleRow ruleRow, TextField expressionField, bool isCondition) {
    var result = ExpressionParser.Instance.Parse(expressionField.value, ruleRow.ActiveBuilding);
    var error = result.LastError;
    if (error == null) {
      if (isCondition) {
        if (result.ParsedExpression is not BoolOperatorExpr) {
          error = _uiFactory.T(ConditionMustBeBoolLocKey);
        }
      } else {
        if (result.ParsedExpression is not ActionExpr) {
          error = _uiFactory.T(ActionMustBeActionLocKey);
        }
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
