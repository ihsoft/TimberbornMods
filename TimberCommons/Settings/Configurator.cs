// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.TimberCommons.Settings;

[Context("MainMenu")]
[Context("Game")]
sealed class Configurator : IConfigurator {
  internal static string ModId => "Timberborn.IgorZ.TimberCommons";

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<TimeAndDurationSettings>().AsSingleton();
    containerDefinition.Bind<IrrigationSystemSettings>().AsSingleton();
    containerDefinition.Bind<WaterBuildingsSettings>().AsSingleton();
    containerDefinition.Bind<DebugSettings>().AsSingleton();
  }
}
