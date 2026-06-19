// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Settings;
using Timberborn.AutomationUI;
using Timberborn.Localization;
using Timberborn.TooltipSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.AutomationSystemUI;

sealed class GameAutomationConflictGuardService(
    ILoc loc,
    ITooltipRegistrar tooltipRegistrar,
    GameAutomationConflictDetector conflictDetector) {

  const string WarningLocKey = "IgorZ.Automation.GameAutomationConflict.Warning";

  public void InitializeSelector(TransmitterSelector transmitterSelector) {
    tooltipRegistrar.RegisterUpdatable(transmitterSelector, () => GetWarningTooltip(transmitterSelector));
    UpdateSelectorState(transmitterSelector);
  }

  public void UpdateSelectorState(TransmitterSelector transmitterSelector) {
    var enabled = !EntityPanelSettings.PreventGameAutomationConflicts || !HasConflict(transmitterSelector);
    SetEnabled(transmitterSelector.Q<Button>("Selection"), enabled);
    SetEnabled(transmitterSelector.Q<Button>("ArrowDown"), enabled);
  }

  string GetWarningTooltip(TransmitterSelector transmitterSelector) {
    return EntityPanelSettings.PreventGameAutomationConflicts && HasConflict(transmitterSelector)
        ? loc.T(WarningLocKey)
        : null;
  }

  bool HasConflict(TransmitterSelector transmitterSelector) {
    var owner = transmitterSelector._owner;
    return owner && conflictDetector.HasConflictingRules(owner.GetComponent<AutomationBehavior>());
  }

  static void SetEnabled(VisualElement element, bool enabled) {
    element?.SetEnabled(enabled);
  }
}
