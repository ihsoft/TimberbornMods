// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;

namespace Automation.Conditions {

public sealed class DroughtEndedCondition : WeatherTrackerConditionBase {
  #region WeatherTrackerConditionBase implemenantion
  /// <inheritdoc/>
  public override string UiDescription => "<SolidHighlight>drought ended</SolidHighlight>";

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
