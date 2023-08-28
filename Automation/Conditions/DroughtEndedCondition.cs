// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;

namespace Automation.Conditions {

/// <summary>Condition that checks if the drought season has ended.</summary>
/// <remarks>The state change only happens when a notification is sent from the game.</remarks>
// ReSharper disable once UnusedType.Global
public sealed class DroughtEndedCondition : WeatherTrackerConditionBase {
  const string DescriptionLocKey = "IgorZ.Automation.DroughtEndedCondition.Description";

  #region WeatherTrackerConditionBase implemenantion
  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey);

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new DroughtEndedCondition();
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return true;
  }

  /// <inheritdoc/>
  protected override void OnWeatherChanged(bool isDrought) {
    ConditionState = !isDrought;
  }
  #endregion
}

}
