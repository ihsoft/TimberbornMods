// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class WeatherScriptableComponent : ScriptableComponentBase, IPostLoadableSingleton {

  const string SeasonSignalLocKey = "IgorZ.Automation.Scriptable.Weather.Signal.Season";

  const string SeasonSignalName = "Weather.Season";
  const string DroughtSeason = "drought";
  const string BadTideSeason = "badtide";
  const string TemperateSeason = "temperate";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Weather";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(BaseComponent _) {
    return [SeasonSignalName]; 
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
        SeasonSignalName => SeasonSignalDef,
        _ => throw new ScriptError("Unknown signal: " + name)
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(string name, BaseComponent building, Action onValueChanged) {
    if (!_signalChangeCallbacks.TryGetValue(name, out var callbacks)) {
      callbacks = [];
      _signalChangeCallbacks[name] = callbacks;
    }
    callbacks.Add(onValueChanged);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(string name, BaseComponent building, Action onValueChanged) {
    if (_signalChangeCallbacks.TryGetValue(name, out var callbacks)) {
      callbacks.Remove(onValueChanged);
    }
  }

  #endregion

  #region IPostLoadableSingleton implementation

  public void PostLoad() {
    _currentSeason = GetCurrentSeason();
  }

  #endregion

  #region Signals

  SignalDef SeasonSignalDef => _seasonSignalDef ??= new SignalDef {
      ScriptName = SeasonSignalName,
      DisplayName = Loc.T(SeasonSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.String,
          Options = [
              (TemperateSeason, Loc.T("Weather.Temperate")),
              (DroughtSeason, Loc.T("Weather.Drought")),
              (BadTideSeason, Loc.T("Weather.Badtide")),
          ],
      },
  };
  SignalDef _seasonSignalDef;

  #endregion

  #region Implementation

  readonly WeatherService _weatherService;
  readonly HazardousWeatherService _hazardousWeatherService;

  string _currentSeason;
  readonly Dictionary<string, List<Action>> _signalChangeCallbacks = new();

  WeatherScriptableComponent(
      EventBus eventBus, WeatherService weatherService, HazardousWeatherService hazardousWeatherService) {
    _weatherService = weatherService;
    _hazardousWeatherService = hazardousWeatherService;
    eventBus.Register(this);
    _currentSeason = GetCurrentSeason();
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

  void NotifySignalChanged(string name) {
    if (_signalChangeCallbacks.TryGetValue(name, out var callbacks)) {
      foreach (var callback in callbacks) {
        callback();
      }
    }
  }

  #endregion

  #region Event listeners

  [OnEvent]
  public void OnHazardousWeatherStartedEvent(HazardousWeatherStartedEvent @event) {
    _currentSeason = @event.HazardousWeather switch {
        DroughtWeather => DroughtSeason,
        BadtideWeather => BadTideSeason,
        _ => throw new InvalidOperationException("Unknown hazardous weather type: " + @event.HazardousWeather),
    };
    NotifySignalChanged(SeasonSignalName);
  }

  [OnEvent]
  public void OnHazardousWeatherEndedEvent(HazardousWeatherEndedEvent @event) {
    _currentSeason = TemperateSeason;
    NotifySignalChanged(SeasonSignalName);
  }

  #endregion
}
