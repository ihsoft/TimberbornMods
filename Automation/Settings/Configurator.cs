// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

// ReSharper disable once CheckNamespace
namespace IgorZ.Automation.Settings;

[Context("MainMenu")]
[Context("Game")]
sealed class Configurator : IConfigurator {
  public const string AutomationModId = "Timberborn.IgorZ.Automation";

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<AutomationDebugSettings>().AsSingleton();
    containerDefinition.Bind<EntityPanelSettings>().AsSingleton();
  }
}
