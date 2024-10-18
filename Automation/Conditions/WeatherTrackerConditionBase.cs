// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using TimberApi.DependencyContainerSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;

namespace IgorZ.Automation.Conditions;

/// <summary>The base class for the conditions that need to react on the weather season change.</summary>
/// <remarks>The state change only happens when a notification is sent from the game.</remarks>
public abstract class WeatherTrackerConditionBase : AutomationConditionBase {
  #region AutomationConditionBase overrides
  /// <inheritdoc/>
  public override void SyncState() {
    var weatherService = DependencyContainer.GetInstance<WeatherService>();
    if (weatherService.IsHazardousWeather) {
      var hazardousWeatherService = DependencyContainer.GetInstance<HazardousWeatherService>();
      if (hazardousWeatherService.CurrentCycleHazardousWeather is DroughtWeather) {
        OnWeatherChanged(isDrought: true, isBadtide: null);
      } else if (hazardousWeatherService.CurrentCycleHazardousWeather is BadtideWeather) {
        OnWeatherChanged(isDrought: null, isBadtide: true);
      }
    } else {
      OnWeatherChanged(isDrought: false, isBadtide: false);
    }
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
  /// <param name="isDrought">Indicates if drought season started or ended.</param>
  /// <param name="isBadtide">Indicates if badtide season started or ended.</param>
  protected abstract void OnWeatherChanged(bool? isDrought, bool? isBadtide);
  #endregion

  #region Implemenatation
  /// <summary>Triggers when weather season changes to drought.</summary>
  [OnEvent]
  public void OnDroughtStartedEvent(HazardousWeatherStartedEvent @event) {
    switch (@event.HazardousWeather) {
      case DroughtWeather:
        OnWeatherChanged(isDrought: true, isBadtide: null);
        break;
      case BadtideWeather:
        OnWeatherChanged(isDrought: null, isBadtide: true);
        break;
    }
  }

  /// <summary>Triggers when weather season changes to temperate.</summary>
  [OnEvent]
  public void OnDroughtEndedEvent(HazardousWeatherEndedEvent @event) {
    switch (@event.HazardousWeather) {
      case DroughtWeather:
        OnWeatherChanged(isDrought: false, isBadtide: null);
        break;
      case BadtideWeather:
        OnWeatherChanged(isDrought: null, isBadtide: false);
        break;
    }
  }
  #endregion
}