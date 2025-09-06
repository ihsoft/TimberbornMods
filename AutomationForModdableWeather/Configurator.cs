// Timberborn Mod: AutomationForModdableWeather
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.AutomationForModdableWeather;

// ReSharper disable once UnusedType.Global
[Context("Game")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<ModdableWeatherSupport>().AsSingleton();
  }
}
