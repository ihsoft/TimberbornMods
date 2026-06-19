namespace IgorZ.Automation.Conditions;

using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;

public abstract class AutomationConditionBase : IAutomationCondition {
  public AutomationBehavior Behavior { get; set; }
  public bool Enabled { get; set; } = true;
  public bool IsEnabled => Enabled;
  public bool CanRunOnUnfinishedBuildings => false;
  public bool IsMarkedForCleanup => false;
  public bool IsInErrorState => false;
  public string UiDescription => "";

  public void Activate() {
  }

  public virtual IAutomationCondition CloneDefinition() {
    throw new System.NotSupportedException();
  }
}

sealed class ScriptedCondition : AutomationConditionBase {
  public string Expression { get; private set; }
  public ParsingResult ParsingResult { get; set; }

  public void SetExpression(string expression) {
    Expression = expression;
  }
}
