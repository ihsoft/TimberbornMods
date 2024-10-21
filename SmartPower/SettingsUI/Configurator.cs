// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.EntityPanelSystem;

namespace IgorZ.SmartPower.SettingsUI;

[Context("Game")]
sealed class Configurator : IConfigurator {
  class EntityPanelModuleProvider(DebugFragment debugFragment) : IProvider<EntityPanelModule> {
    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddDiagnosticFragment(debugFragment);
      return builder.Build();
    }
  }

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<DebugFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }
}
