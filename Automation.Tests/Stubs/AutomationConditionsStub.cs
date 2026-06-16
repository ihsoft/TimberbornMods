namespace IgorZ.Automation.Conditions;

using IgorZ.Automation.AutomationSystem;

public abstract class AutomationConditionBase : IAutomationCondition {
  public AutomationBehavior Behavior { get; set; }
  public bool IsEnabled => true;
  public bool CanRunOnUnfinishedBuildings => false;
  public bool IsMarkedForCleanup => false;

  public void Activate() {
  }

  public virtual IAutomationCondition CloneDefinition() {
    throw new System.NotSupportedException();
  }
}

sealed class ScriptedCondition : AutomationConditionBase {
  public string Expression { get; private set; }

  public void SetExpression(string expression) {
    Expression = expression;
  }
}
