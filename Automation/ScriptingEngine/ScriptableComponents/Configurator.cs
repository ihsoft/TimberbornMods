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
    HarmonyPatcher.PatchRepeated(PatchId, typeof(FloodgatePatch));

    // The order of bindings is npt important for the scripting engine, but in the constructor the siganls/ and actions
    // will be shown in the order of bindings.

    // The building-specific components. Order them from the most to the less frequently needed. 
    containerDefinition.Bind<FloodgateScriptableComponent>().AsSingleton();
    containerDefinition.Bind<PausableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<InventoryScriptableComponent>().AsSingleton();
    containerDefinition.Bind<ConstructableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<PrioritizableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<DynamiteScriptableComponent>().AsSingleton();
    containerDefinition.Bind<StreamGaugeScriptableComponent>().AsSingleton();
    containerDefinition.Bind<FlowControlScriptableComponent>().AsSingleton();

    // Global components. Order them from the most to the less frequently needed.
    containerDefinition.Bind<WeatherScriptableComponent>().AsSingleton();
    containerDefinition.Bind<DistrictScriptableComponent>().AsSingleton();
    containerDefinition.Bind<SignalsScriptableComponent>().AsSingleton();
    containerDefinition.Bind<DebugScriptableComponent>().AsSingleton();

    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<StreamGauge, StreamGaugeScriptableComponent.StreamGaugeCheckTicker>();
    return builder.Build();
  }
}
