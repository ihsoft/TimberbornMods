// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.EntityPanelSystem;

namespace IgorZ.Automation.ScriptingEngineUI;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<AutomationScriptFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }

  sealed class EntityPanelModuleProvider(AutomationScriptFragment automationFragment) : IProvider<EntityPanelModule> {
    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(automationFragment);
      return builder.Build();
    }
  }
}