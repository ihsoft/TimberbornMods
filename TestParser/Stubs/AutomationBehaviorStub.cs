using System;
using IgorZ.Automation.Actions;
using IgorZ.Automation.Conditions;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;

namespace IgorZ.Automation.AutomationSystem;

public class AutomationBehavior : BaseComponent {
  public BlockObject BlockObject => throw new NotImplementedException();
  public T GetOrCreate<T>() where T : AbstractDynamicComponent {
    throw new NotImplementedException();
  }
  public T GetOrThrow<T>() where T : AbstractDynamicComponent {
    throw new NotImplementedException();
  }
  public void AddRule(ScriptedCondition condition, ScriptedAction action) {
    throw new NotImplementedException();
  }
}
