// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.MapBrowser.Core;

[Context("MainMenu")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<WorkshopMetadataService>().AsSingleton();
    containerDefinition.Bind<WorkshopLiveDetailsService>().AsSingleton();
    containerDefinition.Bind<WorkshopSubscriptionService>().AsSingleton();
  }
}
