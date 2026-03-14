// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.TemplateInstantiation;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

// ReSharper disable once UnusedType.Global
[Context("Game")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<SignalDispatcher>().AsTransient();
    containerDefinition.Bind<ReferenceManager>().AsTransient();

    // The building-specific components.
    containerDefinition.Bind<CollectableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<CollectableScriptableComponent.GatherableTracker>().AsTransient();
    containerDefinition.Bind<ConstructableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<ConstructableScriptableComponent.ConstructableStateTracker>().AsTransient();
    containerDefinition.Bind<DynamiteScriptableComponent>().AsSingleton();
    containerDefinition.Bind<DynamiteScriptableComponent.DynamiteStateController>().AsTransient();
    containerDefinition.Bind<FloodgateScriptableComponent>().AsSingleton();
    containerDefinition.Bind<FloodgateScriptableComponent.HeightChangeTracker>().AsTransient();
    containerDefinition.Bind<FlowControlScriptableComponent>().AsSingleton();
    containerDefinition.Bind<InventoryScriptableComponent>().AsSingleton();
    containerDefinition.Bind<InventoryScriptableComponent.EmptyingStatusBehavior>().AsTransient();
    containerDefinition.Bind<InventoryScriptableComponent.InventoryChangeTracker>().AsTransient();
    containerDefinition.Bind<ManufactoryScriptableComponent>().AsSingleton();
    containerDefinition.Bind<PausableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<PlantableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<PlantableScriptableComponent.PlantableTracker>().AsTransient();
    containerDefinition.Bind<PrioritizableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<StreamGaugeScriptableComponent>().AsSingleton();
    containerDefinition.Bind<StreamGaugeScriptableComponent.StreamGaugeCheckTicker>().AsTransient();
    containerDefinition.Bind<StreamGaugeScriptableComponent.StreamGaugeTracker>().AsTransient();
    containerDefinition.Bind<WorkplaceScriptableComponent>().AsSingleton();
    containerDefinition.Bind<WorkplaceScriptableComponent.WorkplaceChangeTracker>().AsSingleton();

    // Global components.
    containerDefinition.Bind<DebugScriptableComponent>().AsSingleton();
    containerDefinition.Bind<DistrictScriptableComponent>().AsSingleton();
    containerDefinition.Bind<DistrictScriptableComponent.DistrictChangeTracker>().AsTransient();
    containerDefinition.Bind<SignalsScriptableComponent>().AsSingleton();
    containerDefinition.Bind<WeatherScriptableComponent>().AsSingleton();

    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<StreamGauge, StreamGaugeScriptableComponent.StreamGaugeCheckTicker>();
    return builder.Build();
  }
}
