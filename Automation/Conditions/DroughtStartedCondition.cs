// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;

namespace Automation.Conditions {

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
  protected override void OnWeatherChanged(bool isDrought) {
    ConditionState = isDrought;
  }
  #endregion
}

}
