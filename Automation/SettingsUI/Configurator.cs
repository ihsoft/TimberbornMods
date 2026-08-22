// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using ModSettings.CoreUI;

namespace IgorZ.Automation.SettingsUI;

[Context("MainMenu")]
[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  /// <inheritdoc />
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.MultiBind<IModSettingElementFactory>()
        .To<SignalModuleOrderSettingElementFactory>()
        .AsSingleton();
  }
}

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class GameConfigurator : IConfigurator {
  /// <inheritdoc />
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<SignalModuleOrderDialog>().AsTransient();
  }
}
