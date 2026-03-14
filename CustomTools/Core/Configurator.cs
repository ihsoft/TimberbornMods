// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using ConfigurableToolGroups.UI;

// ReSharper disable once CheckNamespace
namespace IgorZ.CustomTools.Core;

[Context("Game")]
class Configurator : IConfigurator {

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<CustomToolsService>().AsSingleton();
    containerDefinition.Bind<FeatureLimiterService>().AsSingleton();
    containerDefinition.MultiBind<CustomBottomBarElement>().To<LayoutElementLeft>().AsSingleton();
    containerDefinition.MultiBind<CustomBottomBarElement>().To<LayoutElementMiddle>().AsSingleton();
    containerDefinition.MultiBind<CustomBottomBarElement>().To<LayoutElementRight>().AsSingleton();
  }
}
