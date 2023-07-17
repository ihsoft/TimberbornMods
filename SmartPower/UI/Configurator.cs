// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.EntityPanelSystem;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower.UI {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<SmartGoodPoweredGeneratorFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }

  sealed class EntityPanelModuleProvider : IProvider<EntityPanelModule> {
    readonly SmartGoodPoweredGeneratorFragment _automationFragment;

    public EntityPanelModuleProvider(SmartGoodPoweredGeneratorFragment automationFragment) {
      _automationFragment = automationFragment;
    }

    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(_automationFragment);
      return builder.Build();
    }
  }
}

}
