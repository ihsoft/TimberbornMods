// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using TimberApi.SceneSystem;
using Bindito.Core;
using TimberApi.ConfiguratorSystem;
namespace CustomResources {

[Configurator(SceneEntrypoint.MainMenu)]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    AssetPatcher.Patch();
  }
}

}
