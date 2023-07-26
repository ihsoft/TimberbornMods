// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Automation.Core;
using Timberborn.BaseComponentSystem;

namespace Automation.Conditions {

//FXIME  make it generic to control teh base type
public class AutomationConditionBehaviorBase : BaseComponent {
  protected IEnumerable<IAutomationCondition> Conditions => _conditions.AsReadOnly();
  readonly List<IAutomationCondition> _conditions = new();

  public void AddCondition(AutomationConditionBase automationCondition) {
    _conditions.Add(automationCondition);
  }

  public void DeleteCondition(AutomationConditionBase automationCondition) {
    _conditions.Remove(automationCondition);
    if (_conditions.Count == 0) {
      Destroy(this);
    }
  }
}

}
