// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
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

  /// <summary>A list of season IDs that are returned "as-is" and are not supported by the constructor.</summary>
  /// <remarks>
  /// Extend this list as more weather affecting mods are being released/updated. The IDs that are not known will be
  /// reported in the logs.
  /// </remarks>
  static readonly HashSet<string> ThirdPartySeasons = [
      // Moddable Weather: https://steamcommunity.com/workshop/filedetails/?id=3493039008
      "Monsoon", "ProgressiveTemperate", "Rain", "ShortTemperate", "SurprisinglyRefreshing",
  ];

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Weather";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior _) {
    return [SeasonSignalName]; 
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior _) {
    return name switch {
        SeasonSignalName => SeasonSignal,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior _) {
    return name switch {
        SeasonSignalName => SeasonSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName != SeasonSignalName) {
      throw new InvalidOperationException("Unknown signal: " + signalOperator.SignalName);
    }
    _referenceManager.AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    _referenceManager.RemoveSignal(signalOperator, host);
  }

  #endregion

  #region IPostLoadableSingleton implementation

  public void PostLoad() {
    _eventBus.Register(this);
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

  ScriptValue SeasonSignal() {
    return ScriptValue.Of(_currentSeason);
  }

  #endregion

  #region Implementation

  readonly EventBus _eventBus;
  readonly WeatherService _weatherService;
  readonly HazardousWeatherService _hazardousWeatherService;
  readonly string _badtideWeatherId;
  readonly string _droughtWeatherId;

  string _currentSeason;
  readonly ReferenceManager _referenceManager = new();

  WeatherScriptableComponent(EventBus eventBus, WeatherService weatherService,
                             HazardousWeatherService hazardousWeatherService,
                             BadtideWeather badtideWeather, DroughtWeather droughtWeather) {
    _eventBus = eventBus;
    _weatherService = weatherService;
    _hazardousWeatherService = hazardousWeatherService;
    _badtideWeatherId = badtideWeather.Id;
    _droughtWeatherId = droughtWeather.Id;
  }

  /// <summary>Gets the current season based on the weather conditions.</summary>
  /// <remarks>
  /// Don't use in hazardous season end events due to the wayher service state is not fully updated yet.
  /// </remarks>
  /// <exception cref="InvalidOperationException">if the weather season can't be recognized.</exception>
  string GetCurrentSeason() {
    if (!_weatherService.IsHazardousWeather) {
      return TemperateSeason;
    }
    var weatherId = _hazardousWeatherService.CurrentCycleHazardousWeather.Id;
    if (weatherId == _badtideWeatherId) {
      return BadTideSeason;
    }
    if (weatherId == _droughtWeatherId) {
      return DroughtSeason;
    }
    if (!ThirdPartySeasons.Contains(weatherId)) {
      DebugEx.Warning(
          "[Automation system] Unrecognized hazardous weather ID: {0}. Returning it as a season name.", weatherId);
    }
    return weatherId;
  }

  #endregion

  #region Event listeners

  [OnEvent]
  public void OnHazardousWeatherStartedEvent(HazardousWeatherStartedEvent _) {
    _currentSeason = GetCurrentSeason();
    _referenceManager.ScheduleSignal(SeasonSignalName, ScriptingService, ignoreErrors: true);
  }

  [OnEvent]
  public void OnHazardousWeatherEndedEvent(HazardousWeatherEndedEvent @event) {
    _currentSeason = TemperateSeason;
    _referenceManager.ScheduleSignal(SeasonSignalName, ScriptingService, ignoreErrors: true);
  }

  #endregion
}
