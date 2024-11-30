// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.EntityPanelSystem;

namespace IgorZ.SmartPower.PowerConsumersUI;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<PowerInputLimiterFragment>().AsSingleton();
    containerDefinition.Bind<SmartManufactoryFragment>().AsSingleton();
    containerDefinition.Bind<ConsumerFragmentPatcher>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }

  sealed class EntityPanelModuleProvider(PowerInputLimiterFragment powerInputLimiterFragment,
                                         SmartManufactoryFragment smartManufactoryFragment)
      : IProvider<EntityPanelModule> {
    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddMiddleFragment(powerInputLimiterFragment);
      builder.AddMiddleFragment(smartManufactoryFragment);
      return builder.Build();
    }
  }
}
