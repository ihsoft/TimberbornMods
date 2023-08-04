// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;
using Timberborn.Emptying;
using UnityDev.Utils.LogUtilsLite;

namespace Automation.Actions {

/// <summary>Action that disables the storage empty mode.</summary>
public class UnmarkForEmptyingAction : AutomationActionBase {
  #region AutomationActionBase overrides
  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new UnmarkForEmptyingAction { TemplateFamily = TemplateFamily };
  }

  /// <inheritdoc/>
  public override string UiDescription => "<SolidHighlight>stop emptying storage</SolidHighlight>";

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return base.IsValidAt(behavior) && behavior.GetComponentFast<Emptiable>() != null;
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState) {
      return;
    }
    var component = Behavior.GetComponentFast<Emptiable>();
    if (component.IsMarkedForEmptying) {
      DebugEx.Fine("Unmark for emptying: {0}", Behavior);
      Behavior.GetComponentFast<Emptiable>().UnmarkForEmptying();
    }
  }
  #endregion
}

}
