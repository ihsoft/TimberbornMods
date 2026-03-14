// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.PowerGeneration;
using Timberborn.TemplateInstantiation;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace IgorZ.SmartPower.PowerGenerators;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.ApplyPatch(PatchId, typeof(GoodPoweredGeneratorPatch));
    containerDefinition.Bind<SmartGoodConsumingGenerator>().AsTransient();
    containerDefinition.Bind<SmartWalkerPoweredGenerator>().AsTransient();
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    // Add decorator to the component, not to spec! We need pre-determined execution order in Tick.
    builder.AddDecorator<GoodPoweredGenerator, SmartGoodConsumingGenerator>();
    builder.AddDecorator<WalkerPoweredGenerator, SmartWalkerPoweredGenerator>();
    return builder.Build();
  }
}
