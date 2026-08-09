// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.EntityPanelSystem;
using Timberborn.NeedApplication;
using Timberborn.TemplateInstantiation;

namespace IgorZ.TimberCommons.NeedApplierUI;

[Context("Game")]
sealed class Configurator : IConfigurator {
  class EntityPanelModuleProvider(InjuryProbabilityFragment injuryProbabilityFragment) : IProvider<EntityPanelModule> {
    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddTopFragment(injuryProbabilityFragment);
      return builder.Build();
    }
  }

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<InjuryProbabilityFragment>().AsSingleton();
    containerDefinition.Bind<WorkshopInjuryStatistics>().AsTransient();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<WorkshopRandomNeedApplierSpec, WorkshopInjuryStatistics>();
    return builder.Build();
  }
}
