// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.UI {

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<UiFactory>().AsSingleton();
    containerDefinition.Bind<PanelFragment>().AsSingleton();
    containerDefinition.Bind<GameButtonDeprecated>().AsSingleton();
  }
}

}
