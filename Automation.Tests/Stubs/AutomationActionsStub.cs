using System;
using IgorZ.Automation.ScriptingEngine.Parser;

namespace IgorZ.Automation.Actions;

public abstract class AutomationActionBase : IgorZ.Automation.AutomationSystem.IAutomationAction {
  public static readonly object ActionSerializer = new();
  public static readonly object ActionSerializerNullable = new();

  public IgorZ.Automation.AutomationSystem.AutomationBehavior Behavior { get; set; }
  public IgorZ.Automation.AutomationSystem.IAutomationCondition Condition { get; set; }
  public virtual string TemplateFamily { get; set; } = "";
  public virtual bool IsMarkedForCleanup => false;

  public virtual IgorZ.Automation.AutomationSystem.IAutomationAction CloneDefinition() {
    throw new NotSupportedException();
  }
}

sealed class ScriptedAction : AutomationActionBase {
  public string Expression { get; set; }
  public ParsingResult ParsingResult { get; set; }

  public void SetExpression(string expression) {
    Expression = expression;
  }
}
