// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;
using Timberborn.BuildingsBlocking;

namespace Automation.Actions {

/// <summary>Action that resumes a pausable building.</summary>
/// <remarks>Due to any construction site is pausable, this action can only be applied to a finished building.</remarks>
public class UnpauseAction : AutomationActionBase {
  #region AutomationActionBase overrides
  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new UnpauseAction();
  }

  /// <inheritdoc/>
  public override string UiDescription => "<SolidHighlight>unpause building</SolidHighlight>";

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    if (!behavior.BlockObject.Finished) {
      return false;
    }
    var component = behavior.GetComponentFast<PausableBuilding>();
    return component != null && component.IsPausable();
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState) {
      return;
    }
    var component = Behavior.GetComponentFast<PausableBuilding>();
    if (component.Paused) {
      component.Resume();
    }
  }
  #endregion
}

}
