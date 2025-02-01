// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngineUI;

interface IEditorProvider {
  public string CreateRuleLocKey { get; }
  public string EditRuleLocKey { get; }
  public void MakeForRule(RuleRow ruleRow);
  public bool VerifyIfEditable(RuleRow ruleRow);
}
