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

  const string SeasonTriggerName = "Season";
  const string DroughtSeason = "drought";
  const string BadTideSeason = "badtide";
  const string TemperateSeason = "temperate";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Weather";

  /// <inheritdoc/>
  public override Type InstanceType => null;

  /// <inheritdoc/>
  public override string[] GetTriggerNamesForBuilding(BaseComponent _) {
    return [SeasonTriggerName]; 
  }

  /// <inheritdoc/>
  public override ITriggerSource GetTriggerSource(string name, BaseComponent _, Action onValueChanged) {
    var trigger = name switch {
        SeasonTriggerName => new SeasonTrigger(this, onValueChanged),
        _ => throw new ScriptError("Unknown trigger: " + name),
    };
    return trigger;
  }

  /// <inheritdoc/>
  public override IScriptable.TriggerDef GetTriggerDefinition(string name, BaseComponent _) {
    return name switch {
        SeasonTriggerName => new IScriptable.TriggerDef {
            FullName = $"{Name}.{SeasonTriggerName}",
            DisplayName = LocTrigger(SeasonTriggerName),
            ValueType = new IScriptable.ArgumentDef {
                ArgumentType = IScriptable.ArgumentDef.Type.String,
                Options = [
                    (TemperateSeason, Loc.T("Weather.Temperate")),
                    (DroughtSeason, Loc.T("Weather.Drought")),
                    (BadTideSeason, Loc.T("Weather.Badtide")),
                ],
            },
        },
        _ => throw new ScriptError("Unknown trigger: " + name)
    };
  }

  #endregion

  #region Implementation

  readonly WeatherService _weatherService;
  readonly HazardousWeatherService _hazardousWeatherService;

  readonly Dictionary<ITriggerSource, Action> _seasonChangeTriggers = [];

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

  #region Season trigger implementation

  sealed class SeasonTrigger : ITriggerSource {

    readonly WeatherScriptableComponent _parent;
    internal static string CurrentSeason;

    public SeasonTrigger(WeatherScriptableComponent parent, Action onValueChanged) {
      _parent = parent;
      _parent._seasonChangeTriggers.Add(this, onValueChanged);
      CurrentSeason = _parent.GetCurrentSeason();
    }

    /// <inheritdoc/>
    public int NumberValue => throw new ScriptError("Season cannot be represented as a number");
    /// <inheritdoc/>
    public string StringValue => CurrentSeason;
    /// <inheritdoc/>
    public void Dispose() => _parent._seasonChangeTriggers.Remove(this);
  }

  #endregion

  #region Event listeners

  [OnEvent]
  public void OnHazardousWeatherStartedEvent(HazardousWeatherStartedEvent @event) {
    SeasonTrigger.CurrentSeason = @event.HazardousWeather switch {
        DroughtWeather => DroughtSeason,
        BadtideWeather => BadTideSeason,
        _ => throw new InvalidOperationException("Unknown hazardous weather type: " + @event.HazardousWeather),
    };
    foreach (var action in _seasonChangeTriggers.Values) {
      action();
    }
  }

  [OnEvent]
  public void OnHazardousWeatherEndedEvent(HazardousWeatherEndedEvent @event) {
    SeasonTrigger.CurrentSeason = TemperateSeason;
    foreach (var action in _seasonChangeTriggers.Values) {
      action();
    }
  }

  #endregion
}
