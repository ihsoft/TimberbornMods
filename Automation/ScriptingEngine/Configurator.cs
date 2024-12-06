// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.Buildings;
using Timberborn.TemplateSystem;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine;

// ReSharper disable once UnusedType.Global
[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    HarmonyPatcher.PatchRepeated(PatchId, typeof(FloodgateSetHeightPatch));
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<Building, AutomationScript>();
    builder.AddDecorator<Floodgate, FloodgateScriptableComponent>();
    return builder.Build();
  }
}