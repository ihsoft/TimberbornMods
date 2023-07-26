// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;

namespace Automation.Conditions {

/// <summary>Base class for an condition that is controlled by a global behavior.</summary>
/// <remarks>Global behaviors run as singletons in <see cref="AutomationService"/>.</remarks>
/// <typeparam name="T">type of the behavior component</typeparam>
public abstract class GlobalConditionBase<T> : AutomationConditionBase where T : AutomationConditionBehaviorBase {
  #region AutomationConditionBase implementanton
  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    Behavior.AutomationService.GetGlobalBehavior<T>().AddCondition(this);
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    Behavior.AutomationService.GetGlobalBehavior<T>().DeleteCondition(this);
  }
  #endregion
}

}
