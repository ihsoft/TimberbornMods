// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.AutomationSystem;
using Timberborn.BuildingsBlocking;

namespace Automation.Actions {

/// <summary>Action that resumes a pausable building.</summary>
/// <remarks>Due to any construction site is pausable, this action can only be applied to a finished building.</remarks>
// ReSharper disable once UnusedType.Global
public class UnpauseAction : AutomationActionBase {
  const string DescriptionLocKey = "IgorZ.Automation.UnpauseAction.Description";

  #region AutomationActionBase overrides
  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new UnpauseAction { TemplateFamily = TemplateFamily };
  }

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey);

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    var component = behavior.GetComponentFast<PausableBuilding>();
    return component && component.IsPausable();
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
