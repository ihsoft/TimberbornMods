// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.SmartHaulers.Core;
using IgorZ.SmartHaulers.Dispatching;
using IgorZ.SmartHaulers.DispatchingUI;
using IgorZ.TimberDev.UI;
using Timberborn.EntityPanelSystem;
using Timberborn.GameDistricts;
using Timberborn.TemplateInstantiation;

namespace IgorZ.SmartHaulers.Patches;

[Context("Game")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<DispatchCenterRegistry>().AsSingleton();
    containerDefinition.Bind<DispatchPerformanceStats>().AsSingleton();
    containerDefinition.Bind<HaulBehaviorSupportValidator>().AsSingleton();
    containerDefinition.Bind<HaulerDispatchRefreshService>().AsSingleton();
    containerDefinition.Bind<TransportDecisionEvaluator>().AsSingleton();
    containerDefinition.Bind<TransportDistanceEstimator>().AsSingleton();
    containerDefinition.Bind<UiFactory>().AsSingleton();
    containerDefinition.Bind<HaulerDispatchCenter>().AsTransient();
    containerDefinition.Bind<HaulerDispatchDebugPanel>().AsSingleton();
    containerDefinition.Bind<KeyBindingInputProcessor>().AsSingleton();
    containerDefinition.Bind<SmartHaulersPatcher>().AsSingleton();
    containerDefinition.Bind<TransportDebugRowFactory>().AsSingleton();
    containerDefinition.Bind<TransportAgentFragment>().AsSingleton();
    containerDefinition.Bind<TransportRequesterFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<DistrictCenter, HaulerDispatchCenter>();
    return builder.Build();
  }

  sealed class EntityPanelModuleProvider(
      TransportAgentFragment transportAgentFragment,
      TransportRequesterFragment transportRequesterFragment) : IProvider<EntityPanelModule> {
    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddSideFragment(transportAgentFragment);
      builder.AddSideFragment(transportRequesterFragment);
      return builder.Build();
    }
  }
}
