// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;
using IgorZ.TimberDev.UI;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.Actions;

/// <summary>Writes a debug message into the log.</summary>
public class DebugLogAction : AutomationActionBase {

  #region AutomationActionBase overrides

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new DebugLogAction { TemplateFamily = TemplateFamily };
  }

  /// <inheritdoc/>
  public override string UiDescription => CommonFormats.HighlightYellow("write 'hello' into the log");

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return true;
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState) {
      return;
    }
    HostedDebugLog.Warning(Behavior, "[DebugLogAction] HELLO!");
  }

  #endregion
}
