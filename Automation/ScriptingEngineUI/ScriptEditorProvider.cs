// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

class ScriptEditorProvider : IEditorProvider {

  const string ConditionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ConditionLabel";
  const string ActionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ActionLabel";

  const string TestScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.TestScriptBtn";
  const string ConditionMustBeBoolLocKey = "IgorZ.Automation.Scripting.Editor.ConditionMustBeBoolean";
  const string ActionMustBeActionLocKey = "IgorZ.Automation.Scripting.Editor.ActionMustBeAction";

  const int TestScriptStatusHighlightDurationMs = 1000;
  static readonly Color ErrorBackgroundColor = new Color(1, 0, 0, 0.2f);
  static readonly Color GoodScriptTextColor = Color.green;

  readonly UiFactory _uiFactory;
  
  ScriptEditorProvider(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  public void MakeForRule(RuleRow ruleRow) {
    var root = new VisualElement();

    var conditionEdit = _uiFactory.CreateTextField();
    conditionEdit.style.flexGrow = 1;
    conditionEdit.textInput.style.unityTextAlign = TextAnchor.MiddleLeft;
    conditionEdit.value = ruleRow.ConditionExpression ?? "";
    root.Add(CreateRow(_uiFactory.CreateLabel(ConditionLabelLocKey), conditionEdit));

    var actionEdit = _uiFactory.CreateTextField();
    actionEdit.style.flexGrow = 1;
    actionEdit.textInput.style.unityTextAlign = TextAnchor.MiddleLeft;
    actionEdit.value = ruleRow.ActionExpression ?? "";
    root.Add(CreateRow(_uiFactory.CreateLabel(ActionLabelLocKey), actionEdit));

    ruleRow.CreateEditView(
        root,
        () => RunRuleCheck(ruleRow, conditionEdit, actionEdit),
        () => {
          ruleRow.ConditionExpression = conditionEdit.value;
          ruleRow.ActionExpression = actionEdit.value;
        });

    // Test script button.
    ruleRow.CreateButton(TestScriptBtnLocKey, btn => {
      VisualEffects.ScheduleSwitchEffect(
          btn, TestScriptStatusHighlightDurationMs, false, true,
          (b, v) => b.SetEnabled(v));
      if (!RunRuleCheck(ruleRow, conditionEdit, actionEdit)) {
        return;
      }
      VisualEffects.ScheduleSwitchEffect(
          conditionEdit, TestScriptStatusHighlightDurationMs, GoodScriptTextColor, UiFactory.DefaultColor,
          (c, v) => c.textInput.style.color = v);
      VisualEffects.ScheduleSwitchEffect(
          actionEdit, TestScriptStatusHighlightDurationMs, GoodScriptTextColor, UiFactory.DefaultColor,
          (c, v) => c.textInput.style.color = v);
    }, addAtBeginning: true);
  }
  public bool VerifyIfEditable(RuleRow ruleRow) {
    return true;
  }

  bool RunRuleCheck(RuleRow ruleRow, TextField conditionEdit, TextField actionEdit) {
    conditionEdit.textInput.style.color = UiFactory.DefaultColor;
    actionEdit.textInput.style.color = UiFactory.DefaultColor;
    ruleRow.ClearError();
    if (CheckExpressionAndShowError(ruleRow, conditionEdit, true)
        && CheckExpressionAndShowError(ruleRow, actionEdit, false)) {
      return true;
    }
    return false;
  }

  bool CheckExpressionAndShowError(RuleRow ruleRow, TextField expressionField, bool isCondition) {
    var parserContext = new ParserContext() {
        ScriptHost = ruleRow.ActiveBuilding,
    };
    ExpressionParser.Instance.Parse(expressionField.value, parserContext);
    var error = parserContext.LastError;
    if (error == null) {
      if (isCondition) {
        if (parserContext.ParsedExpression is not BoolOperatorExpr) {
          error = _uiFactory.T(ConditionMustBeBoolLocKey);
        }
      } else {
        if (parserContext.ParsedExpression is not ActionExpr) {
          error = _uiFactory.T(ActionMustBeActionLocKey);
        }
      }
    }
    if (error == null) {
      return true;
    }
    ruleRow.ReportError(error);
    VisualEffects.ScheduleSwitchEffect(
        expressionField, TestScriptStatusHighlightDurationMs, ErrorBackgroundColor, Color.clear,
        (f, c) => f.textInput.style.backgroundColor = c);
    return false;
  }

  static VisualElement CreateRow(params VisualElement[] elements) {
    var row = new VisualElement();
    row.style.flexDirection = FlexDirection.Row;
    row.style.alignItems = Align.Center;

    for (var i = 0; i < elements.Length; i++) {
      var element = elements[i];
      if (i != elements.Length - 1) {
        element.style.marginRight = 5;
      }
      row.Add(element);
    }
    return row;
  }
}
