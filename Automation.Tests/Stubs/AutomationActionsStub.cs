using System;

namespace IgorZ.Automation.Actions;

public abstract class AutomationActionBase : IgorZ.Automation.AutomationSystem.IAutomationAction {
  public static readonly object ActionSerializer = new();
  public static readonly object ActionSerializerNullable = new();

  public IgorZ.Automation.AutomationSystem.AutomationBehavior Behavior { get; set; }
  public IgorZ.Automation.AutomationSystem.IAutomationCondition Condition { get; set; }
  public virtual string TemplateFamily => "";
  public virtual bool IsMarkedForCleanup => false;

  public virtual IgorZ.Automation.AutomationSystem.IAutomationAction CloneDefinition() {
    throw new NotSupportedException();
  }
}
