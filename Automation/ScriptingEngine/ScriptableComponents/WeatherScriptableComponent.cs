// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using Timberborn.BlueprintSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class WeatherScriptableComponent : ScriptableComponentBase, IPostLoadableSingleton {

  const string SeasonSignalLocKey = "IgorZ.Automation.Scriptable.Weather.Signal.Season";
  const string TemperateWeatherNameLocKey = "Weather.Temperate";
  const string BadtideWeatherNameLocKey = "Weather.Badtide";
  const string DroughtWeatherNameLocKey = "Weather.Drought";

  const string SeasonSignalName = "Weather.Season";
  const string TemperateWeatherId = "TemperateWeather";

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
    LoadWeatherIds();
  }

  #endregion

  #region Signals

  SignalDef SeasonSignalDef => _seasonSignalDef ??= new SignalDef {
      ScriptName = SeasonSignalName,
      DisplayName = Loc.T(SeasonSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.String,
          Options = _weatherSeasonOptions,
          // FIXME: Options changed in v2.5.6 on 2025-06-16, drop one day.
          CompatibilityOptions = new Dictionary<string, string> {
              { "temperate", TemperateWeatherId },
              { "badtide", "BadtideWeather" },
              { "drought", "DroughtWeather" },
          },
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
  readonly ISpecService _specService;

  static readonly (string Value, string text)[] StandardSeasons = [
      ("DroughtWeather", DroughtWeatherNameLocKey),
      ("BadtideWeather", BadtideWeatherNameLocKey),
      (TemperateWeatherId, TemperateWeatherNameLocKey),
  ];

  string _currentSeason;
  readonly ReferenceManager _referenceManager = new();
  DropdownItem<string>[] _weatherSeasonOptions;

  WeatherScriptableComponent(EventBus eventBus, WeatherService weatherService,
                             HazardousWeatherService hazardousWeatherService, ISpecService specService) {
    _eventBus = eventBus;
    _weatherService = weatherService;
    _hazardousWeatherService = hazardousWeatherService;
    _specService = specService;
  }

  /// <summary>Gets the current season based on the weather conditions.</summary>
  /// <remarks>
  /// Don't use in hazardous season end events due to the weather service state is not fully updated yet.
  /// </remarks>
  /// <exception cref="InvalidOperationException">if the weather season can't be recognized.</exception>
  string GetCurrentSeason() {
    //FIXME
    DebugEx.Warning("GetCurrentSeason: IsHazardousWeather={0}, weatherId={1}",
                    _weatherService.IsHazardousWeather, _hazardousWeatherService.CurrentCycleHazardousWeather.Id);
    
    return _weatherService.IsHazardousWeather
        ? _hazardousWeatherService.CurrentCycleHazardousWeather.Id
        : TemperateWeatherId;
  }


  /// <summary>Loads the weather IDs from the weather specs.</summary>
  /// <remarks>
  /// In case there are modded seasons added by the other mods, they're loaded first. Then, the standard seasons added
  /// if they're missing in the modded list. The order in which the IDs are loaded will be reflected in the constructor
  /// UI.
  /// </remarks>
  void LoadWeatherIds() {
    if (_specService is not SpecService specService) {
      DebugEx.Error("ISpecService expected to be SpecService, but was {0}. Don't load modded seasons.", _specService);
      _weatherSeasonOptions = StandardSeasons
          .Select(x => new DropdownItem<string> { Value = x.Value, Text = Loc.T(x.text) })
          .Reverse()
          .ToArray();
      return;
    }
    var seasons = new List<DropdownItem<string>>();
    var specTypes = specService._cachedBlueprints.Keys.Where(q => q.Name.EndsWith("WeatherSpec"));
    foreach (var specType in specTypes) {
      var specs = (IEnumerable)typeof(ISpecService).GetMethod(nameof(ISpecService.GetSpecs))!
          .MakeGenericMethod(specType).Invoke(specService, []);
      var idProp = specType.GetProperty("Id");
      var idLocNameProp = specType.GetProperty("NameLocKey");
      if (idProp == null || idLocNameProp == null) {
        DebugEx.Warning("Skipping incompatible weather spec {0}: hasId={1}, hasNameLocKey={2}",
                        specType, idProp != null, idLocNameProp != null);
        continue;
      }
      foreach (var spec in specs) {
        var seasonId = idProp.GetValue(spec) as string;
        var seasonNameLocKey = idLocNameProp.GetValue(spec) as string;
        if (string.IsNullOrWhiteSpace(seasonId) || string.IsNullOrWhiteSpace(seasonNameLocKey)) {
          DebugEx.Warning("Skipping bad weather spec {0}: id={1}, nameLocKey={2}",
                          specType, seasonId, seasonNameLocKey);
          continue;
        }
        if (seasons.Any(x => x.Value == seasonId)) {
          DebugEx.Warning("Skipping duplicate weather spec {0}: id={1}, nameLocKey={2}",
                          specType, seasonId, seasonNameLocKey);
          continue;
        }
        DebugEx.Info("Loading weather spec {0}: id={1}, name={2}", specType, seasonId, seasonNameLocKey);
        seasons.Add(new DropdownItem<string> { Value = seasonId, Text = Loc.T(seasonNameLocKey) });
      }
    }
    foreach (var stdSeason in StandardSeasons) {
      if (seasons.All(x => x.Value != stdSeason.Value)) {
        seasons.Insert(0, new DropdownItem<string> { Value = stdSeason.Value, Text = Loc.T(stdSeason.text) });
      }
    }
    _weatherSeasonOptions = seasons.ToArray();
  }

  #endregion

  #region Event listeners

  [OnEvent]
  public void OnHazardousWeatherStartedEvent(HazardousWeatherStartedEvent _) {
    _currentSeason = GetCurrentSeason();
    _referenceManager.ScheduleSignal(SeasonSignalName, ScriptingService, ignoreErrors: true);
  }

  [OnEvent]
  public void OnHazardousWeatherEndedEvent(HazardousWeatherEndedEvent _) {
    _currentSeason = TemperateWeatherId;
    _referenceManager.ScheduleSignal(SeasonSignalName, ScriptingService, ignoreErrors: true);
  }

  #endregion
}
