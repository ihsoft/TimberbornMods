// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;
using IgorZ.TimberDev.UI;
using Timberborn.HazardousWeatherSystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class WeatherScriptableComponent
    : ScriptableComponentBase, IPostLoadableSingleton, ISaveableSingleton, IWeatherExtension {

  const string SeasonSignalLocKey = "IgorZ.Automation.Scriptable.Weather.Signal.Season";
  const string TemperateWeatherNameLocKey = "Weather.Temperate";
  const string BadtideWeatherNameLocKey = "Weather.Badtide";
  const string DroughtWeatherNameLocKey = "Weather.Drought";

  const string SeasonSignalName = "Weather.Season";
  const string TemperateWeatherId = "TemperateWeather";

  sealed class SeasonUpdateNotifier : MonoBehaviour;  // Just a MonoBehaviour to run coroutines.

  // Keep the existing weather save contract while sharing value tracking with configured signals.
  sealed class SeasonTracker(ReferenceManager references)
      : AbstractGlobalStatusTracker(WeatherStateRestorerKey, references) {
    /// <inheritdoc/>
    public override void Load(ISingletonLoader loader) {
      if (loader.TryGetSingleton(WeatherStateRestorerKey, out var component)) {
        var season = component.Get(CurrentSeasonKey);
        if (!string.IsNullOrEmpty(season)) {
          SetSignalValue(SeasonSignalName, ScriptValue.FromString(season));
        }
      }
    }

    /// <inheritdoc/>
    public override void Save(ISingletonSaver saver) {
      if (TryGetSignalValue(SeasonSignalName, out var value)) {
        saver.GetSingleton(WeatherStateRestorerKey).Set(CurrentSeasonKey, value.AsString);
      }
    }
  }

  #region IWeatherExtension implementation

  /// <inheritdoc/>
  public void AddWeatherId(string weatherId, string nameLocKey) {
    if (!_weatherSeasonIds.Add(weatherId)) {
      DebugEx.Warning("Skipping duplicate weather spec: id={0}, nameLocKey={1}", weatherId, nameLocKey);
      return;
    }
    DebugEx.Info("Adding weather spec: id={0}, name={1}", weatherId, nameLocKey);
    _weatherSeasons.Add((weatherId, nameLocKey));
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
    _tracker.Load(_singletonLoader);
  }

  /// <inheritdoc/>
  public void Save(ISingletonSaver singletonSaver) {
    _tracker.Save(singletonSaver);
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
    return name switch { SeasonSignalName => SeasonSignal, _ => throw new UnknownSignalException(name) };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior _) {
    return name switch { SeasonSignalName => SeasonSignalDef, _ => throw new UnknownSignalException(name) };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName != SeasonSignalName) {
      throw new InvalidOperationException($"Unknown signal: {signalOperator.SignalName}");
    }
    _tracker.AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    _tracker.RemoveSignal(signalOperator, host);
  }

  #endregion

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    _eventBus.Register(this);
    if (!_tracker.TryGetSignalValue(SeasonSignalName, out _)) {
      var season = SeasonSignal().AsString;
      DebugEx.Info("Initialized weather season: {0}", season);
    }
  }

  #endregion

  #region Signals

  SignalDef SeasonSignalDef => _seasonSignalDef ??= new SignalDef {
      ScriptName = SeasonSignalName,
      DisplayName = Loc.T(SeasonSignalLocKey),
      Scope = SignalDef.ScopeEnum.Global,
      Result = new ValueDef { ValueType = ScriptValue.TypeEnum.String, Options = GetWeatherSeasonOptions() },
  };
  SignalDef _seasonSignalDef;

  ScriptValue SeasonSignal() {
    return _tracker.GetOrInitialize(SeasonSignalName, () => ScriptValue.FromString(GetCurrentSeason()));
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
  readonly HashSet<string> _weatherSeasonIds = [TemperateWeatherId, "BadtideWeather", "DroughtWeather"];
  // Extensible list of functions to get the current temperate weather ID.
  readonly List<Func<string>> _weatherIdProviders = [];

  readonly SeasonTracker _tracker;
  readonly MonoBehaviour _monoBehaviour;

  WeatherScriptableComponent(EventBus eventBus, WeatherService weatherService, ReferenceManager referenceManager,
      HazardousWeatherService hazardousWeatherService, ISingletonLoader singletonLoader,
      AutomationExtensionsRegistry automationExtensionsRegistry) {
    _eventBus = eventBus;
    _weatherService = weatherService;
    _tracker = new SeasonTracker(referenceManager);
    _hazardousWeatherService = hazardousWeatherService;
    _singletonLoader = singletonLoader;
    _monoBehaviour = new GameObject("WeatherScriptableComponent").AddComponent<SeasonUpdateNotifier>();
    automationExtensionsRegistry.RegisterExtension(nameof(IWeatherExtension), this);
  }

  DropdownItem[] GetWeatherSeasonOptions() {
    return _weatherSeasons.Select(x => new DropdownItem { Value = x.Value, Text = Loc.T(x.text) }).Reverse().ToArray();
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
    var oldSeason = SeasonSignal().AsString;
    var currentSeason = GetCurrentSeason();
    if (oldSeason != currentSeason) {
      DebugEx.Info("Weather season changed: {0} -> {1}", oldSeason, currentSeason);
      if (_tracker.HasSignalListeners(SeasonSignalName) && !_weatherSeasonIds.Contains(currentSeason)) {
        DebugEx.Warning("Unknown weather season ID: {0}. Automation rules won't trigger.", currentSeason);
      }
      _tracker.TriggerSignalUpdate(SeasonSignalName, ScriptValue.FromString(currentSeason));
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
