// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;

namespace Automation.Conditions {

/// <summary>Condition that checks if the badtide season has started.</summary>
/// <remarks>The state change only happens when a notification is sent from the game.</remarks>
/// ReSharper disable once UnusedType.Global
public sealed class BadtideStartedCondition : WeatherTrackerConditionBase {
  const string DescriptionLocKey = "IgorZ.Automation.BadtideStartedCondition.Description";

  #region WeatherTrackerConditionBase
  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey);

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new BadtideStartedCondition();
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return true;
  }

  /// <inheritdoc/>
  protected override void OnWeatherChanged(bool? isDrought, bool? isBadtide) {
    if (isBadtide.HasValue) {
      ConditionState = isBadtide.Value;
    }
  }
  #endregion
}

}
