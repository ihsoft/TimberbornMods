// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using Timberborn.Buildings;
using Timberborn.TemplateSystem;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine;

// ReSharper disable once UnusedType.Global
[Context("Game")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    containerDefinition.Bind<ScriptingService>().AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    //FIXME: optimize and only add to the buildings that can be automated or provide triggers.
    builder.AddDecorator<Building, AutomationScript>();
    builder.AddDecorator<Floodgate, FloodgateScriptableComponent>();
    return builder.Build();
  }
}
