// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.SmartHaulers.Core;
using IgorZ.SmartHaulers.Dispatching;
using IgorZ.SmartHaulers.DispatchingUI;
using Timberborn.EntityPanelSystem;
using Timberborn.GameDistricts;
using Timberborn.TemplateInstantiation;

namespace IgorZ.SmartHaulers.Patches;

[Context("Game")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<DispatchCenterRegistry>().AsSingleton();
    containerDefinition.Bind<HaulerDispatchCenter>().AsTransient();
    containerDefinition.Bind<HaulerDispatchDebugPanel>().AsSingleton();
    containerDefinition.Bind<KeyBindingInputProcessor>().AsSingleton();
    containerDefinition.Bind<TransportAgentFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<DistrictCenter, HaulerDispatchCenter>();
    return builder.Build();
  }

  sealed class EntityPanelModuleProvider(TransportAgentFragment transportAgentFragment) : IProvider<EntityPanelModule> {
    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddSideFragment(transportAgentFragment);
      return builder.Build();
    }
  }
}
