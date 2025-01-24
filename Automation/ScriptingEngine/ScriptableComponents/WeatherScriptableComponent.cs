// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class WeatherScriptableComponent : ScriptableComponentBase {

  const string SeasonSignalLocKey = "IgorZ.Automation.Scriptable.Weather.Signal.Season";

  const string SeasonSignalName = "Season";
  const string DroughtSeason = "drought";
  const string BadTideSeason = "badtide";
  const string TemperateSeason = "temperate";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Weather";

  /// <inheritdoc/>
  public override Type InstanceType => null;

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(BaseComponent _) {
    return [$"{Name}.{SeasonSignalName}"]; 
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, BaseComponent _) {
    return name switch {
        SeasonSignalName => () => ScriptValue.Of(_currentSeason),
        _ => throw new ScriptError("Unknown signal: " + name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, BaseComponent _) {
    return name switch {
        SeasonSignalName => new SignalDef {
            FullName = $"{Name}.{SeasonSignalName}",
            DisplayName = Loc.T(SeasonSignalLocKey),
            ResultType = new ArgumentDef {
                ValueType = ScriptValue.TypeEnum.String,
                Options = [
                    (TemperateSeason, Loc.T("Weather.Temperate")),
                    (DroughtSeason, Loc.T("Weather.Drought")),
                    (BadTideSeason, Loc.T("Weather.Badtide")),
                ],
            },
        },
        _ => throw new ScriptError("Unknown signal: " + name)
    };
  }

  public override void Load() {
    base.Load();
    _currentSeason = GetCurrentSeason();
  }

  #endregion

  #region Implementation

  readonly WeatherService _weatherService;
  readonly HazardousWeatherService _hazardousWeatherService;

  WeatherScriptableComponent(
      EventBus eventBus, WeatherService weatherService, HazardousWeatherService hazardousWeatherService) {
    _weatherService = weatherService;
    _hazardousWeatherService = hazardousWeatherService;
    eventBus.Register(this);
  }

  /// <summary>
  /// Gets the current season based on the weather conditions. Don't use it in the weather change event handlers!
  /// </summary>
  /// <exception cref="InvalidOperationException">if the weather season can't be recognized.</exception>
  string GetCurrentSeason() {
    if (!_weatherService.IsHazardousWeather) {
      return TemperateSeason;
    }
    return _hazardousWeatherService.CurrentCycleHazardousWeather switch {
        DroughtWeather => DroughtSeason,
        BadtideWeather => BadTideSeason,
        _ => throw new InvalidOperationException(
            "Unknown hazardous weather type: " + _hazardousWeatherService.CurrentCycleHazardousWeather),
    };
  }

  #endregion

  #region Event listeners

  string _currentSeason;

  [OnEvent]
  public void OnHazardousWeatherStartedEvent(HazardousWeatherStartedEvent @event) {
    _currentSeason = @event.HazardousWeather switch {
        DroughtWeather => DroughtSeason,
        BadtideWeather => BadTideSeason,
        _ => throw new InvalidOperationException("Unknown hazardous weather type: " + @event.HazardousWeather),
    };
    ScriptingService.NotifySignalChanged($"{Name}.{SeasonSignalName}");
  }

  [OnEvent]
  public void OnHazardousWeatherEndedEvent(HazardousWeatherEndedEvent @event) {
    _currentSeason = TemperateSeason;
    ScriptingService.NotifySignalChanged($"{Name}.{SeasonSignalName}");
  }

  #endregion
}
