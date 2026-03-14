// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.WaterBuildings;
using Timberborn.WorkSystem;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class InvertRuleButtonProvider(ParserFactory parserFactory) : IEditorButtonProvider {

  const string InvertRuleBtnLocKey = "IgorZ.Automation.Scripting.Editor.InvertRuleBtn";

  /// <inheritdoc/>
  public string CreateRuleBtnLocKey => null;

  /// <inheritdoc/>
  public string RuleRowBtnLocKey => InvertRuleBtnLocKey;

  /// <inheritdoc/>
  public void OnRuleRowBtnAction(RuleRow ruleRow) {
    var inverted = parserFactory.DefaultParser.InvertBooleanExpression(ruleRow.ParsedCondition, ruleRow.ActiveBuilding);
    ruleRow.ConditionExpression = parserFactory.DefaultParser.Decompile(inverted);
    var invertedActionExpression = MakeInvertedActionExpression(ruleRow.ParsedAction, ruleRow.ActiveBuilding);
    if (invertedActionExpression != null) {
      var result = parserFactory.LispSyntaxParser.Parse(invertedActionExpression, ruleRow.ActiveBuilding);
      if (result.LastScriptError != null) {
        throw new InvalidOperationException(
            $"Cannot parse inverted action: {invertedActionExpression} => {result.LastError}");
      }
      ruleRow.ActionExpression = parserFactory.DefaultParser.Decompile(result.ParsedExpression); 
    }
    ruleRow.SwitchToViewMode();
  }

  /// <inheritdoc/>
  public bool IsRuleRowBtnEnabled(RuleRow ruleRow) {
    return ruleRow.ParsedCondition != null && ruleRow.ParsedAction != null;
  }

  string MakeInvertedActionExpression(ActionOperator action, AutomationBehavior automationBehavior) {
    var actioName = action.ActionName;
    if (actioName == "Floodgate.SetHeight") {
      var floodgate = automationBehavior.GetComponent<Floodgate>();
      var argHeight = ((IValueExpr)action.Operands[0]).ValueFn().AsInt;
      if (argHeight == 0) {
        return $"(act Floodgate.SetHeight {floodgate.MaxHeight * 100})";
      }
      if (argHeight == floodgate.MaxHeight) {
        return "(act Floodgate.SetHeight 0)";
      }
      return null;
    }
    if (actioName is "Workplace.SetWorkers" or "Workplace.RemoveWorkers") {
      var workplace = automationBehavior.GetComponent<Workplace>();
      var argNumWorkers = actioName == "Workplace.SetWorkers" ? ((IValueExpr)action.Operands[0]).ValueFn().AsInt : 0;
      if (argNumWorkers == 0) {
        return $"(act Workplace.SetWorkers {workplace.MaxWorkers * 100})";
      }
      if (argNumWorkers == workplace.MaxWorkers) {
        return "(act Workplace.RemoveWorkers)";
      }
      return null;
    }
    return action.ActionName switch {
        "FlowControl.Open" => "(act FlowControl.Close)",
        "FlowControl.Close" => "(act FlowControl.Open)",
        "Inventory.StartEmptying" => "(act Inventory.StopEmptying)",
        "Inventory.StopEmptying" => "(act Inventory.StartEmptying)",
        "Pausable.Pause" => "(act Pausable.Unpause)",
        "Pausable.Unpause" => "(act Pausable.Pause)",
        "Prioritizable.SetHaulers" => "(act Prioritizable.ResetHaulers)",
        "Prioritizable.ResetHaulers" => "(act Prioritizable.SetHaulers)",
        _ => null,
    };
  }
}
