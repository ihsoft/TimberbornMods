// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
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
  public override ISignalSource GetSignalSource(string name, BaseComponent _, Action onValueChanged) {
    var signal = name switch {
        SeasonSignalName => new SeasonSignal(this, onValueChanged),
        _ => throw new ScriptError("Unknown signal: " + name),
    };
    return signal;
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, BaseComponent _) {
    return name switch {
        SeasonSignalName => new SignalDef {
            FullName = $"{Name}.{SeasonSignalName}",
            DisplayName = SeasonSignalLocKey,
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

  #endregion

  #region Implementation

  readonly WeatherService _weatherService;
  readonly HazardousWeatherService _hazardousWeatherService;

  readonly Dictionary<ISignalSource, Action> _seasonChangeSignals = [];

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

  #region Season signal implementation

  sealed class SeasonSignal : ISignalSource {

    readonly WeatherScriptableComponent _parent;
    internal static string CurrentSeason;

    public SeasonSignal(WeatherScriptableComponent parent, Action onValueChanged) {
      _parent = parent;
      if (onValueChanged != null) {
        _parent._seasonChangeSignals.Add(this, onValueChanged);
      }
      CurrentSeason = _parent.GetCurrentSeason();
    }

    /// <inheritdoc/>
    public ScriptValue CurrentValue => ScriptValue.Of(CurrentSeason);

    /// <inheritdoc/>
    public void Dispose() => _parent._seasonChangeSignals.Remove(this);
  }

  #endregion

  #region Event listeners

  [OnEvent]
  public void OnHazardousWeatherStartedEvent(HazardousWeatherStartedEvent @event) {
    SeasonSignal.CurrentSeason = @event.HazardousWeather switch {
        DroughtWeather => DroughtSeason,
        BadtideWeather => BadTideSeason,
        _ => throw new InvalidOperationException("Unknown hazardous weather type: " + @event.HazardousWeather),
    };
    foreach (var action in _seasonChangeSignals.Values) {
      action();
    }
  }

  [OnEvent]
  public void OnHazardousWeatherEndedEvent(HazardousWeatherEndedEvent @event) {
    SeasonSignal.CurrentSeason = TemperateSeason;
    foreach (var action in _seasonChangeSignals.Values) {
      action();
    }
  }

  #endregion
}
