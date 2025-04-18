// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;
using UnityDev.Utils.LogUtilsLite;

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
  public override void RegisterSignalChangeCallback(string name, ISignalListener host) {
    if (name != SeasonSignalName) {
      throw new ScriptError("Unknown signal: " + name);
    }
    var callback = new ScriptingService.SignalCallback(name, host);
    if (!_signalChangeCallbacks.Add(callback)) {
      throw new InvalidOperationException("Signal callback already registered: " + callback);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(string name, ISignalListener host) {
    var callback = new ScriptingService.SignalCallback(name, host);
    if (!_signalChangeCallbacks.Remove(callback)) {
      DebugEx.Warning("Signal callback is not registered: {0}", callback);
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
  readonly HashSet<ScriptingService.SignalCallback> _signalChangeCallbacks = [];

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

  #endregion

  #region Event listeners

  [OnEvent]
  public void OnHazardousWeatherStartedEvent(HazardousWeatherStartedEvent @event) {
    _currentSeason = @event.HazardousWeather switch {
        DroughtWeather => DroughtSeason,
        BadtideWeather => BadTideSeason,
        _ => throw new InvalidOperationException("Unknown hazardous weather type: " + @event.HazardousWeather),
    };
    foreach (var callback in _signalChangeCallbacks) {
      ScriptingService.ScheduleSignalCallback(callback);
    }
  }

  [OnEvent]
  public void OnHazardousWeatherEndedEvent(HazardousWeatherEndedEvent @event) {
    _currentSeason = TemperateSeason;
    foreach (var callback in _signalChangeCallbacks) {
      ScriptingService.ScheduleSignalCallback(callback);
    }
  }

  #endregion
}
