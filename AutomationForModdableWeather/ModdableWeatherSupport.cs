// Timberborn Mod: AutomationForModdableWeather
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using Bindito.Core;
using IgorZ.Automation.Api;
using ModdableWeathers.Cycles;
using ModdableWeathers.Services;
using ModdableWeathers.Weathers;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.AutomationForModdableWeather;

sealed class ModdableWeatherSupport : IPostLoadableSingleton, IUnloadableSingleton {
  const string ModNameLocKey = "IgorZ.AutomationForModdableWeather.Name";
  const string SeasonNameLocKey = "IgorZ.AutomationForModdableWeather.Season";
  const string SeasonSignalId = "Season";

  /// <inheritdoc/>
  public void PostLoad() {
    var options = _registry.Weathers
        .Where(weather => !string.IsNullOrEmpty(weather.Spec.NameLocKey))
        .Select(weather => (weather.Id, weather.Spec.NameLocKey))
        .ToArray();
    _automation.AddSignal(SeasonSignalId, SeasonNameLocKey, () => _weather.CurrentWeather.Id, options);
    _eventBus.Register(this);
    DebugEx.Info("[Automation Plug-In] Declared independent Moddable Weathers season signal.");
  }

  /// <inheritdoc/>
  public void Unload() {
    _eventBus.Unregister(this);
  }

  /// <summary>Queues a season update after the weather mod has switched its current weather.</summary>
  [OnEvent]
  public void OnWeatherTransitioned(WeatherTransitionedEvent _) {
    _automation.UpdateSignal(SeasonSignalId);
  }

  readonly ModdableWeatherService _weather;
  readonly ModdableWeatherRegistry _registry;
  readonly EventBus _eventBus;
  readonly AutomationApi _automation;

  ModdableWeatherSupport(
      ModdableWeatherService weather, ModdableWeatherRegistry registry, EventBus eventBus, IContainer container) {
    _weather = weather;
    _registry = registry;
    _eventBus = eventBus;
    _automation = new AutomationApi(container, "ModdableWeathers", ModNameLocKey);
  }
}
