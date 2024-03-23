// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.AutomationSystem;

namespace Automation.Conditions {

/// <summary>Condition that checks if the drought season has started.</summary>
/// <remarks>The state change only happens when a notification is sent from the game.</remarks>
/// ReSharper disable once UnusedType.Global
public sealed class DroughtStartedCondition : WeatherTrackerConditionBase {
  const string DescriptionLocKey = "IgorZ.Automation.DroughtStartedCondition.Description";

  #region WeatherTrackerConditionBase
  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey);

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new DroughtStartedCondition();
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return true;
  }

  /// <inheritdoc/>
  protected override void OnWeatherChanged(bool? isDrought, bool? isBadtide) {
    if (isDrought.HasValue) {
      ConditionState = isDrought.Value;
    }
  }
  #endregion
}

}
