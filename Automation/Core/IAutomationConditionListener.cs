// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace Automation.Core {

/// <summary>Targets that want to react on a condition state change must implement this interface.</summary>
public interface IAutomationConditionListener {
  /// <summary>Callback that is called by <see cref="IAutomationCondition"/> when the state is updated.</summary>
  public void OnConditionState(IAutomationCondition automationCondition);
}
 
}
