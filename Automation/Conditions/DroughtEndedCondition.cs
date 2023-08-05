// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;

namespace Automation.Conditions {

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
  protected override void OnWeatherChanged(bool isDrought) {
    ConditionState = !isDrought;
  }
  #endregion
}

}
