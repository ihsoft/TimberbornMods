// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using TimberApi.DependencyContainerSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.SingletonSystem;

namespace Automation.Conditions {

/// <summary>The base class for the conditions that need to react on the weather season change.</summary>
/// <remarks>The state change only happens when a notification is sent from the game.</remarks>
public abstract class WeatherTrackerConditionBase : AutomationConditionBase {
  #region AutomationConditionBase overrides
  /// <inheritdoc/>
  public override void SyncState() {
    var weatherService = DependencyContainer.GetInstance<HazardousWeatherService>();
    OnWeatherChanged(isDrought: weatherService.CurrentCycleHazardousWeather is DroughtWeather);
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
  /// <summary>Triggers when weather season changes to drought.</summary>
  [OnEvent]
  public void OnDroughtStartedEvent(HazardousWeatherStartedEvent @event) {
    OnWeatherChanged(isDrought: @event.HazardousWeather is DroughtWeather);
  }

  /// <summary>Triggers when weather season changes to temperate.</summary>
  [OnEvent]
  public void OnDroughtEndedEvent(HazardousWeatherEndedEvent @event) {
    OnWeatherChanged(isDrought: false);
  }
  #endregion
}

}
