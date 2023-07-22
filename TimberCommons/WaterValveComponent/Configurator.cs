// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.EntityPanelSystem;

namespace IgorZ.TimberCommons.WaterValveComponent {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<WaterValveFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }

  sealed class EntityPanelModuleProvider : IProvider<EntityPanelModule> {
    readonly WaterValveFragment _waterValveFragment;

    public EntityPanelModuleProvider(WaterValveFragment waterValveFragment) {
      _waterValveFragment = waterValveFragment;
    }

    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(_waterValveFragment);
      return builder.Build();
    }
  }
}

}
