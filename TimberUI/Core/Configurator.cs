// Timberborn Mod: TimberUI
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.UI;

namespace IgorZ.TimberUI.Core;

[Context("MainMenu")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {

  public const string ModId = "Timberborn.IgorZ.TimberUI";

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<MainDialog>().AsSingleton();
    containerDefinition.Bind<UiFactory>().AsSingleton();
    containerDefinition.Bind<TimberUISettings>().AsSingleton();
  }
}
