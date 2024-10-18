// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;

namespace IgorZ.Automation.Conditions {

/// <summary>Condition that checks if the badtide season has ended.</summary>
/// <remarks>The state change only happens when a notification is sent from the game.</remarks>
// ReSharper disable once UnusedType.Global
public sealed class BadtideEndedCondition : WeatherTrackerConditionBase {
  const string DescriptionLocKey = "IgorZ.Automation.BadtideEndedCondition.Description";

  #region WeatherTrackerConditionBase implemenantion
  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey);

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new BadtideEndedCondition();
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return true;
  }

  /// <inheritdoc/>
  protected override void OnWeatherChanged(bool? isDrought, bool? isBadtide) {
    if (isBadtide.HasValue) {
      ConditionState = !isBadtide.Value;
    }
  }
  #endregion
}

}
