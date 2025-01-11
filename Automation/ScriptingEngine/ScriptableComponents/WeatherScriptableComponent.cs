// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class WeatherScriptableComponent : ILoadableSingleton, IScriptable {

  const string TriggerNameLocKeyPrefix = "IgorZ.Automation.Scripting.Weather.Trigger.";
  const string SeasonTriggerName = "Season";
  const string DroughtSeason = "drought";
  const string BadTideSeason = "badtide";
  const string TemperateSeason = "temperate";

  #region IScriptable implementation

  /// <inheritdoc/>
  public string Name => "Weather";

  /// <inheritdoc/>
  public ITriggerSource GetTriggerSource(string name, BaseComponent building, Action onValueChanged) {
    var trigger = name switch {
        SeasonTriggerName => new SeasonTrigger(this, onValueChanged),
        _ => throw new ScriptError("Unknown trigger: " + name),
    };
    return trigger;
  }

  /// <inheritdoc/>
  public IScriptable.TriggerDef GetTriggerDefinition(string name) {
    var triggerDef = _conditions.FirstOrDefault(c => c.Name == name);
    if (triggerDef == null) {
      throw new ScriptError("Unknown trigger: " + name);
    }
    return triggerDef;
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {}

  #endregion

  #region Implementation

  readonly ILoc _loc;
  readonly WeatherService _weatherService;
  readonly HazardousWeatherService _hazardousWeatherService;

  readonly Dictionary<ITriggerSource, Action> _seasonChangeTriggers = new();
  readonly List<IScriptable.TriggerDef> _conditions = [];

  WeatherScriptableComponent(ILoc loc, WeatherService weatherService, HazardousWeatherService hazardousWeatherService,
                             EventBus eventBus) {
    _loc = loc;
    _weatherService = weatherService;
    _hazardousWeatherService = hazardousWeatherService;
    eventBus.Register(this);

    _conditions.Add(new IScriptable.TriggerDef {
        Name = SeasonTriggerName,
        DisplayName = LocTrigger(SeasonTriggerName),
        ValueType = new IScriptable.ArgumentDef {
            ArgumentType = IScriptable.ArgumentDef.Type.String,
            Options = [
                (TemperateSeason, _loc.T("Weather.Temperate")),
                (DroughtSeason, _loc.T("Weather.Drought")),
                (BadTideSeason, _loc.T("Weather.Badtide")),
            ],
        },
    });
  }

  string LocTrigger(string name) {
    return _loc.T(TriggerNameLocKeyPrefix + name);
  }

  DropdownItem<string> LocTriggerValue(string triggerName, string value) {
    return (value, _loc.T(TriggerNameLocKeyPrefix + triggerName + "." + value));
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
