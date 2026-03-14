// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ScriptEditorButtonProvider : IEditorButtonProvider {

  const string AddRuleFromScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.AddRuleFromScriptBtn";
  const string EditAsScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.EditAsScriptBtn";

  const int TestScriptStatusHighlightDurationMs = 1000;
  const string GoodScriptClass = "text-field-green";
  const string BadScriptClass = "text-field-error";

  #region IEditorProvider implementation

  /// <inheritdoc/>
  public string CreateRuleBtnLocKey => AddRuleFromScriptBtnLocKey;

  /// <inheritdoc/>
  public string RuleRowBtnLocKey => EditAsScriptBtnLocKey;

  /// <inheritdoc/>
  public void OnRuleRowBtnAction(RuleRow ruleRow) {
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
  public bool IsRuleRowBtnEnabled(RuleRow ruleRow) {
    return ruleRow.LegacyAction == null;
  }

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  readonly ParserFactory _parserFactory;
  
  ScriptEditorButtonProvider(UiFactory uiFactory, ParserFactory parserFactory) {
    _uiFactory = uiFactory;
    _parserFactory = parserFactory;
  }

  bool RunRuleCheck(RuleRow ruleRow, TextField conditionEdit, TextField actionEdit) {
    ruleRow.ClearError();
    return CheckConditionExpressionAndShowError(ruleRow, conditionEdit)
        && CheckActionExpressionAndShowError(ruleRow, actionEdit);
  }

  bool CheckConditionExpressionAndShowError(RuleRow ruleRow, TextField expressionField) {
    _parserFactory.ParseCondition(expressionField.value, ruleRow.ActiveBuilding, out var parseResult);
    if (parseResult.LastScriptError == null) {
      return true;
    }
    ruleRow.ReportError(parseResult.LastScriptError);
    VisualEffects.SetTemporaryClass(expressionField, TestScriptStatusHighlightDurationMs, BadScriptClass);
    return false;
  }

  bool CheckActionExpressionAndShowError(RuleRow ruleRow, TextField expressionField) {
    _parserFactory.ParseAction(expressionField.value, ruleRow.ActiveBuilding, out var parseResult);
    if (parseResult.LastScriptError == null) {
      return true;
    }
    ruleRow.ReportError(parseResult.LastScriptError);
    VisualEffects.SetTemporaryClass(expressionField, TestScriptStatusHighlightDurationMs, BadScriptClass);
    return false;
  }

  #endregion
}
