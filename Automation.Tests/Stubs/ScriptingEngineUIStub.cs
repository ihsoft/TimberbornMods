using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RuleRow {
  public BooleanOperator ParsedCondition { get; set; }
  public ActionOperator ParsedAction { get; set; }
  public AutomationBehavior ActiveBuilding { get; set; }
  public string ConditionExpression { get; set; }
  public string ActionExpression { get; set; }

  public void SwitchToViewMode() {
  }
}
