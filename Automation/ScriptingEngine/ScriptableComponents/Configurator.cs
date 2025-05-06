// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

// ReSharper disable once UnusedType.Global
[Context("Game")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    // The order of bindings is npt important for the scripting engine, but in the constructor the siganls/ and actions
    // will be shown in the order of bindings.

    // The building specific components. Order them from the most to the less frequently needed. 
    containerDefinition.Bind<FloodgateScriptableComponent>().AsSingleton();
    containerDefinition.Bind<PausableScriptableComponent>().AsSingleton();
    containerDefinition.Bind<InventoryScriptableComponent>().AsSingleton();

    // Global components. Order them from the most to the less frequently needed.
    containerDefinition.Bind<WeatherScriptableComponent>().AsSingleton();
    containerDefinition.Bind<DistrictScriptableComponent>().AsSingleton();
    containerDefinition.Bind<SignalsScriptableComponent>().AsSingleton();
    containerDefinition.Bind<DebugScriptableComponent>().AsSingleton();
  }
}
