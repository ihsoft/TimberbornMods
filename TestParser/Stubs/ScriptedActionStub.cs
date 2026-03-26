using System;

using IgorZ.Automation.AutomationSystem;

namespace IgorZ.Automation.Actions;

public class ScriptedAction {
  public AutomationBehavior Behavior => throw new NotImplementedException();
  public string Expression => throw new NotImplementedException();
  public IAutomationCondition Condition => throw new NotImplementedException();
}
