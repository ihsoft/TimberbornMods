// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.Automation.ScriptingEngine.Expressions;

namespace IgorZ.Automation.AutomationSystem;

sealed class GameAutomationRuleSaveConflictDetector(GameAutomationConflictDetector conflictDetector) {

  public List<int> GetConflictingRuleNumbers(bool gameAutomationEnabled, IEnumerable<RuleCandidate> rules) {
    var conflictingRules = new List<int>();
    if (!gameAutomationEnabled) {
      return conflictingRules;
    }
    foreach (var rule in rules) {
      if (IsStateChangingRule(rule)) {
        conflictingRules.Add(rule.RuleNumber);
      }
    }
    return conflictingRules;
  }

  public bool IsStateChangingRule(RuleCandidate rule) {
    return !rule.IsDeleted
        && rule.IsEnabled
        && conflictDetector.IsBuildingStateChangingAction(rule.ParsedAction);
  }

  public readonly record struct RuleCandidate(int RuleNumber, bool IsDeleted, bool IsEnabled, ActionOperator ParsedAction);
}
