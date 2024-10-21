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
    containerDefinition.Bind<DebugSettings>().AsSingleton();
    containerDefinition.Bind<NetworkUISettings>().AsSingleton();
  }
}
