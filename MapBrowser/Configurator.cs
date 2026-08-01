using Bindito.Core;
using IgorZ.TimberDev.UI;

namespace IgorZ.MapBrowser;

[Context("MainMenu")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<MainMenuMapBrowserButton>().AsSingleton();
    containerDefinition.Bind<MapBrowserDialog>().AsSingleton();
    containerDefinition.Bind<WorkshopMetadataService>().AsSingleton();
    containerDefinition.Bind<UiFactory>().AsSingleton();
  }
}
