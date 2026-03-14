// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.Automation.CommonTools;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<CancelTool>().AsSingleton();
    containerDefinition.Bind<EnableRulesTool>().AsSingleton();
    containerDefinition.Bind<DisableRulesTool>().AsSingleton();
    containerDefinition.Bind<DebugPickTool>().AsSingleton();
  }
}
