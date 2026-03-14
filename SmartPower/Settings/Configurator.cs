// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.SmartPower.Settings;

[Context("MainMenu")]
[Context("Game")]
sealed class Configurator : IConfigurator {
  internal static string ModId => "Timberborn.IgorZ.SmartPower";

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<BatteriesSettings>().AsSingleton();
    containerDefinition.Bind<WalkerPoweredGeneratorSettings>().AsSingleton();
    containerDefinition.Bind<GoodConsumingGeneratorSettings>().AsSingleton();
    containerDefinition.Bind<WorkplaceConsumerSettings>().AsSingleton();
    containerDefinition.Bind<UnmannedConsumerSettings>().AsSingleton();
    containerDefinition.Bind<AttractionConsumerSettings>().AsSingleton();
    containerDefinition.Bind<SmartPowerDebugSettings>().AsSingleton();
  }
}
