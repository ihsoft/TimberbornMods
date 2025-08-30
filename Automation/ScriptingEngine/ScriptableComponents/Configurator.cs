// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.TemplateSystem;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

// ReSharper disable once UnusedType.Global
[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(PatchId, typeof(FloodgatePatch), typeof(NoUnemployedStatusPatch));
    containerDefinition.Bind<SignalDispatcher>().AsTransient();

    // The building-specific components. 
    containerDefinition.Bind<ConstructableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<DynamiteScriptableComponent>().AsSingleton();
    containerDefinition.Bind<FloodgateScriptableComponent>().AsSingleton();
    containerDefinition.Bind<FlowControlScriptableComponent>().AsSingleton();
    containerDefinition.Bind<InventoryScriptableComponent>().AsSingleton();
    containerDefinition.Bind<PausableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<PlantableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<PrioritizableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<StreamGaugeScriptableComponent>().AsSingleton();
    containerDefinition.Bind<WorkplaceScriptableComponent>().AsSingleton();
    containerDefinition.Bind<CollectableScriptableComponent>().AsSingleton();

    // Global components.
    containerDefinition.Bind<DebugScriptableComponent>().AsSingleton();
    containerDefinition.Bind<DistrictScriptableComponent>().AsSingleton();
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
