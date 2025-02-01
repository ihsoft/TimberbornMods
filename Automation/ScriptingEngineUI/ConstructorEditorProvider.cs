// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngineUI;

class ConstructorEditorProvider : IEditorProvider {

  const string StringConstantTypeLocKey = "IgorZ.Automation.Scripting.Editor.StringConstantType";
  const string NumberConstantTypeLocKey = "IgorZ.Automation.Scripting.Editor.NumberConstantType";

  #region API

  public void MakeForRule(RuleRow ruleRow) {
    var ruleConstructor = new RuleConstructor(_uiFactory);
    PopulateConstructor(ruleRow.ActiveBuilding, ruleConstructor);
    PopulateCondition(ruleRow, ruleConstructor);
    PopulateAction(ruleRow, ruleConstructor);

    ruleRow.CreateEditView(ruleConstructor.Root, () => {
      ruleRow.ClearError();
      var error = ruleConstructor.ConditionConstructor.Validate() ?? ruleConstructor.ActionConstructor.Validate();
      if (error != null) {
        ruleRow.ReportError(error);
      }
      return error == null;
    }, () => {
      ruleRow.ConditionExpression = ruleConstructor.ConditionConstructor.GetScript();
      ruleRow.ActionExpression = ruleConstructor.ActionConstructor.GetScript();
    });
  }

  public bool VerifyIfEditable(RuleRow ruleRow) {
    if (ruleRow.ParsedCondition == null || ruleRow.ParsedAction == null) {
      return false;
    }
    if (ruleRow.ParsedCondition is not BinaryOperatorExpr or BinaryOperatorExpr { Right: not ConstantValueExpr }) {
      return false;
    }
    if (ruleRow.ParsedAction.Operands.Count != 2 || ruleRow.ParsedAction.Operands[1] is not ConstantValueExpr) {
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
            Argument = (t.ScriptName, t.DisplayName),
            ArgumentType = t.Result.ValueType,
            ArgumentOptions = t.Result.Options,
        });
    ruleConstructor.ConditionConstructor.SetDefinitions(conditions);

    var actions = _scriptingService.GetActionNamesForBuilding(behavior)
        .Select(t => _scriptingService.GetActionDefinition(t, behavior))
        .Select(t => new ActionConstructor.ActionDefinition {
            Action = (t.ScriptName, t.DisplayName),
            ArgumentType = t.Arguments[0].ValueType,
            ArgumentOptions = GetArgumentOptions(t.Arguments),
        });
    ruleConstructor.ActionConstructor.SetDefinitions(actions);
  }

  DropdownItem<string>[] GetArgumentOptions(IList<ValueDef> valueDefs) {
    if (valueDefs.Count != 1) {
      throw new InvalidOperationException("Exactly one argument is expected");
    }
    var def = valueDefs[0];
    if (def.Options != null) {
      return def.Options;
    }
    return def.ValueType == ScriptValue.TypeEnum.Number
        ? [(ArgumentConstructor.InputTypeName, _uiFactory.T(NumberConstantTypeLocKey))]
        : [(ArgumentConstructor.InputTypeName, _uiFactory.T(StringConstantTypeLocKey))];
  }

  static void PopulateAction(RuleRow ruleRow, RuleConstructor ruleConstructor) {
    if (ruleRow.ParsedCondition == null) {
      return;
    }
    var actionConstructor = ruleConstructor.ActionConstructor;
    if (ruleRow.ParsedAction.Operands.Count != 2) {
      throw new InvalidOperationException("Exactly two operands are expected");
    }
    actionConstructor.ActionSelector.Value = ruleRow.ParsedAction.ActionName;
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
    if (ruleRow.ParsedCondition is not BinaryOperatorExpr binaryOperatorExpr) {
      throw new InvalidOperationException("Binary operator is expected, but found: " + ruleRow.ParsedCondition);
    }
    conditionConstructor.SignalSelector.Value = binaryOperatorExpr.Left.SignalName;
    conditionConstructor.OperatorSelector.Value = binaryOperatorExpr.Name;
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
