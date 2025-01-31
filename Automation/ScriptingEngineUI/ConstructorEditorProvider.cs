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
using Timberborn.CoreUI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

class ConstructorEditorProvider {

  const string StringConstantTypeLocKey = "IgorZ.Automation.Scripting.Editor.StringConstantType";
  const string NumberConstantTypeLocKey = "IgorZ.Automation.Scripting.Editor.NumberConstantType";

  #region API

  public void MakeForRule(RulesEditorDialog.RuleDefinition rule, AutomationBehavior activeBuilding,
                          out Func<string> applyFn) {
    rule.RuleRow.Q("SidePanel").ToggleDisplayStyle(false);
    var content = rule.RuleRow.Q("RuleContent");
    content.Clear();

    var ruleConstructor = new RuleConstructor(_uiFactory);
    PopulateConstructor(activeBuilding, ruleConstructor);
    content.Add(ruleConstructor.Root);

    applyFn = () => {
      if (ruleConstructor.ConditionConstructor.Validate() != null) {
        DebugEx.Warning("Condition is not valid: {0}", ruleConstructor.ConditionConstructor.Validate());
        return null;
      }
      var error = ruleConstructor.ActionConstructor.Validate();
      if (error != null) {
        return error;
      }
      rule.ConditionExpression = ruleConstructor.ConditionConstructor.GetScript();
      rule.ActionExpression = ruleConstructor.ActionConstructor.GetScript();
      return null;
    };

    PopulateCondition(rule, ruleConstructor);
    PopulateAction(rule, ruleConstructor);
  }

  public bool VerifyIfEditable(RulesEditorDialog.RuleDefinition rule) {
    if (rule.ParsedCondition == null || rule.ParsedAction == null) {
      return false;
    }
    if (rule.ParsedCondition is BinaryOperatorExpr { Right: not ConstantValueExpr }) {
      return false;
    }
    if (rule.ParsedAction.Operands.Count != 2 || rule.ParsedAction.Operands[1] is not ConstantValueExpr) {
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

  static void PopulateAction(RulesEditorDialog.RuleDefinition rule, RuleConstructor ruleConstructor) {
    if (rule.ParsedCondition == null) {
      return;
    }
    var actionConstructor = ruleConstructor.ActionConstructor;
    if (rule.ParsedAction.Operands.Count != 2) {
      throw new InvalidOperationException("Exactly two operands are expected");
    }
    actionConstructor.ActionSelector.Value = rule.ParsedAction.ActionName;
    if (rule.ParsedAction.Operands[1] is not ConstantValueExpr constantValue) {
      throw new InvalidOperationException("Constant value is expected");
    }
    var rightValue = constantValue.ValueFn();
    actionConstructor.ArgumentConstructor.Value = rightValue.ValueType == ScriptValue.TypeEnum.Number
        ? (rightValue.AsNumber / 100f).ToString("0.##")
        : rightValue.AsString;
  }

  static void PopulateCondition(RulesEditorDialog.RuleDefinition rule, RuleConstructor ruleConstructor) {
    if (rule.ParsedCondition == null) {
      return;
    }
    var conditionConstructor = ruleConstructor.ConditionConstructor;
    if (rule.ParsedCondition is not BinaryOperatorExpr binaryOperatorExpr) {
      throw new InvalidOperationException("Binary operator is expected");
    }
    conditionConstructor.SignalSelector.Value = binaryOperatorExpr.Left.SignalName;
    conditionConstructor.OperatorSelector.Value = binaryOperatorExpr.Name;
    if (binaryOperatorExpr.Right is not ConstantValueExpr constantValue) {
      throw new InvalidOperationException("Constant value is expected");
    }
    var rightValue = constantValue.ValueFn();
    conditionConstructor.ValueSelector.Value = rightValue.ValueType == ScriptValue.TypeEnum.Number
        ? (rightValue.AsNumber / 100f).ToString("0.##")
        : rightValue.AsString;
  }

  #endregion
}
