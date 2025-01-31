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

    var root = new RuleConstructor(_uiFactory);
    PopulateConstructor(activeBuilding, root);
    content.Add(root.Root);

    applyFn = () => {
      if (root.ConditionConstructor.Validate() != null) {
        DebugEx.Warning("Condition is not valid: {0}", root.ConditionConstructor.Validate());
        return null;
      }
      var error = root.ActionConstructor.Validate();
      if (error != null) {
        return error;
      }
      rule.ConditionExpression = root.ConditionConstructor.GetScript();
      rule.ActionExpression = root.ActionConstructor.GetScript();
      return null;
    };
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

  #endregion
}
