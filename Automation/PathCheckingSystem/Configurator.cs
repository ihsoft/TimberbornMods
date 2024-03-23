// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;

namespace Automation.PathCheckingSystem {

// ReSharper disable once UnusedType.Global
[Configurator(SceneEntrypoint.InGame)]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<PathCheckingService>().AsSingleton();
  }
}

}
