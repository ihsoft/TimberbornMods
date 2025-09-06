// Timberborn Mod: AutomationForModdableWeather
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;
using ModdableWeather.Services;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.AutomationForModdableWeather;

sealed class ModdableWeatherSupport : IPostLoadableSingleton {

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    Initialize();
  }

  #endregion

  readonly ModdableWeatherService _moddableWeatherService;
  readonly ModdableWeatherRegistry _moddableWeatherRegistry;
  readonly AutomationExtensionsRegistry _automationExtensionsRegistry;

  ModdableWeatherSupport(ModdableWeatherService moddableWeatherService, ModdableWeatherRegistry moddableWeatherRegistry,
                         AutomationExtensionsRegistry automationExtensionsRegistry) {
    _moddableWeatherService = moddableWeatherService;
    _automationExtensionsRegistry = automationExtensionsRegistry;
    _moddableWeatherRegistry = moddableWeatherRegistry;
  }

  void Initialize() {
    DebugEx.Info("[Automation Plug-In] Installing ModdableWeather...");
    var allWeathers =
        _moddableWeatherRegistry.AllWeathers.Where(weather => !string.IsNullOrEmpty(weather.Spec.NameLocKey));
    var extension = _automationExtensionsRegistry.GetExtension<IWeatherExtension>();
    if (extension == null) {
      DebugEx.Error("Failed to get IWeatherExtension from AutomationExtensionsRegistry");
      return;
    }
    foreach (var weather in allWeathers) {
      extension.AddWeatherId(weather.WeatherId, weather.Spec.NameLocKey);
      weather.OnWeatherActiveChanged += (_, _, _) => {
        extension.TriggerSeasonCheck();
      };
    }
    extension.AddTemperateWeatherIdProvider(() => _moddableWeatherService.CurrentWeather.WeatherId);
  }
}
