// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using TimberApi.DependencyContainerSystem;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;

namespace Automation.Conditions {

public abstract class WeatherTrackerConditionBase : AutomationConditionBase {
  #region AutomationConditionBase overrides
  /// <inheritdoc/>
  public override void SyncState() {
    var droughtService = DependencyContainer.GetInstance<DroughtService>();
    OnWeatherChanged(isDrought: droughtService.IsDrought);
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    Behavior.EventBus.Register(this);
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    Behavior.EventBus.Unregister(this);
  }
  #endregion

  #region API
  /// <summary>Callback that is triggered when weather conditions change.</summary>
  /// <param name="isDrought"></param>
  protected abstract void OnWeatherChanged(bool isDrought);
  #endregion

  #region Implemenatation
  [OnEvent]
  public void OnDroughtStartedEvent(DroughtStartedEvent @event) {
    OnWeatherChanged(isDrought: true);
  }

  [OnEvent]
  public void OnDroughtEndedEvent(DroughtEndedEvent @event) {
    OnWeatherChanged(isDrought: false);
  }
  #endregion
}

}
