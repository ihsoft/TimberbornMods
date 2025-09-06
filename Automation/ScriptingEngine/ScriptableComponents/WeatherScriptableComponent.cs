// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;
using IgorZ.TimberDev.UI;
using Timberborn.HazardousWeatherSystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class WeatherScriptableComponent : ScriptableComponentBase, IPostLoadableSingleton, ISaveableSingleton, IWeatherExtension {

  const string SeasonSignalLocKey = "IgorZ.Automation.Scriptable.Weather.Signal.Season";
  const string TemperateWeatherNameLocKey = "Weather.Temperate";
  const string BadtideWeatherNameLocKey = "Weather.Badtide";
  const string DroughtWeatherNameLocKey = "Weather.Drought";

  const string SeasonSignalName = "Weather.Season";
  const string TemperateWeatherId = "TemperateWeather";

  #region IWeatherExtension implementation

  /// <inheritdoc/>
  public void AddWeatherId(string weatherId, string nameLocKey) {
    if (!_weatherSeasonIds.Add(weatherId)) {
      DebugEx.Warning("Skipping duplicate weather spec: id={0}, nameLocKey={1}", weatherId, nameLocKey);
      return;
    }
    DebugEx.Info("Adding weather spec: id={0}, name={1}", weatherId, nameLocKey);
    _weatherSeasons.Add((weatherId, nameLocKey));
    ;
  }

  /// <inheritdoc/>
  public void AddTemperateWeatherIdProvider(Func<string> getCurrentWeatherIdFunc) {
    DebugEx.Info("Adding weather ID provider: {0}", getCurrentWeatherIdFunc.GetType().AssemblyQualifiedName);
    _weatherIdProviders.Add(getCurrentWeatherIdFunc);
  }

  /// <inheritdoc/>
  public void TriggerSeasonCheck() {
    ScheduleSeasonUpdate();
  }

  #endregion

  #region Persistence implementation

  static readonly SingletonKey WeatherStateRestorerKey = new("WeatherStateRestorer");
  static readonly PropertyKey<string> CurrentSeasonKey = new("CurrentSeason");

  /// <inheritdoc/>
  public override void Load() {
    base.Load();
    if (_singletonLoader.TryGetSingleton(WeatherStateRestorerKey, out var objectLoader)) {
      _currentSeason = objectLoader.Get(CurrentSeasonKey);
    }
  }

  /// <inheritdoc/>
  public void Save(ISingletonSaver singletonSaver) {
    singletonSaver.GetSingleton(WeatherStateRestorerKey).Set(CurrentSeasonKey, _currentSeason);
  }

  #endregion

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

  /// <inheritdoc/>
  public void PostLoad() {
    _eventBus.Register(this);
    // FIXME: Saved state added in v2.6.1 on 2025-09-03, drop one day.
    if (string.IsNullOrEmpty(_currentSeason)) {
      _currentSeason = GetCurrentSeason();
    }
  }

  #endregion

  #region Signals

  SignalDef SeasonSignalDef => _seasonSignalDef ??= new SignalDef {
      ScriptName = SeasonSignalName,
      DisplayName = Loc.T(SeasonSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.String,
          Options = GetWeatherSeasonOptions(),
          // FIXME: Options changed in v2.6.1 on 2025-09-01, drop one day.
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
  readonly ISingletonLoader _singletonLoader;

  // Extensible list of weather seasons.
  readonly List<(string Value, string text)> _weatherSeasons = [
      ("DroughtWeather", DroughtWeatherNameLocKey),
      ("BadtideWeather", BadtideWeatherNameLocKey),
      (TemperateWeatherId, TemperateWeatherNameLocKey),
  ];
  readonly HashSet<string> _weatherSeasonIds = [
      TemperateWeatherId, "BadtideWeather", "DroughtWeather",
  ];
  // Extensible list of functions to get the current temperate weather ID.
  readonly List<Func<string>> _weatherIdProviders = [];

  string _currentSeason;
  readonly ReferenceManager _referenceManager = new();
  readonly MonoBehaviour _monoBehaviour;

  WeatherScriptableComponent(EventBus eventBus, WeatherService weatherService,
                             HazardousWeatherService hazardousWeatherService, ISingletonLoader singletonLoader,
                             AutomationExtensionsRegistry automationExtensionsRegistry) {
    _eventBus = eventBus;
    _weatherService = weatherService;
    _hazardousWeatherService = hazardousWeatherService;
    _singletonLoader = singletonLoader;
    _monoBehaviour = new GameObject("WeatherScriptableComponent").AddComponent<SeasonUpdateNotifier>();
    automationExtensionsRegistry.RegisterExtension(nameof(IWeatherExtension),  this);
  }
  sealed class SeasonUpdateNotifier : MonoBehaviour;  // Just a MonoBehaviour to run coroutines.

  DropdownItem<string>[] GetWeatherSeasonOptions() {
    return _weatherSeasons
        .Select(x => new DropdownItem<string> { Value = x.Value, Text = Loc.T(x.text) })
        .Reverse()
        .ToArray();
  }

  string GetCurrentSeason() {
    string seasonId;
    if (_weatherService.IsHazardousWeather) {
      seasonId = _hazardousWeatherService.CurrentCycleHazardousWeather.Id;
    } else {
      seasonId = _weatherIdProviders.Select(x => x()).FirstOrDefault(x => x != null) ?? TemperateWeatherId;
    }
    return seasonId;
  }

  void ScheduleSeasonUpdate() {
    if (_seasonUpdateCoroutine != null) {
      return;
    }
    _seasonUpdateCoroutine = _monoBehaviour.StartCoroutine(UpdateSeasonCoroutine());
  }
  Coroutine _seasonUpdateCoroutine;

  IEnumerator UpdateSeasonCoroutine() {
    yield return new WaitForEndOfFrame(); // Wait to let the weather service update its state.
    var oldSeason = _currentSeason;
    _currentSeason = GetCurrentSeason();
    if (oldSeason != _currentSeason) {
      DebugEx.Info("Weather season changed: {0} -> {1}", oldSeason, _currentSeason);
      if (!_weatherSeasonIds.Contains(_currentSeason)) {
        DebugEx.Warning("Unknown weather season ID: {0}. Automation rules won't trigger.", _currentSeason);
      }
      _referenceManager.ScheduleSignal(SeasonSignalName, ScriptingService, ignoreErrors: true);
    }
    _seasonUpdateCoroutine = null;
  }

  #endregion

  #region Event listeners

  [OnEvent]
  public void OnHazardousWeatherStartedEvent(HazardousWeatherStartedEvent arg) {
    ScheduleSeasonUpdate();
  }

  [OnEvent]
  public void OnHazardousWeatherEndedEvent(HazardousWeatherEndedEvent arg) {
    ScheduleSeasonUpdate();
  }

  #endregion
}
