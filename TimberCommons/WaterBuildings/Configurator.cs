// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.TemplateInstantiation;
using Timberborn.WaterBuildings;

namespace IgorZ.TimberCommons.WaterBuildings;

[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<AdjustableWaterOutput>().AsTransient();
    containerDefinition.Bind<AdjustableWaterOutputMarker>().AsTransient();
    containerDefinition.MultiBind<TemplateModule>().ToProvider<WaterOutputTemplateModuleProvider>().AsSingleton();
    HarmonyPatcher.ApplyPatch(PatchId, typeof(WaterOutputPatch), typeof(DecoratorDefinitionPatch));
  }

  class WaterOutputTemplateModuleProvider : IProvider<TemplateModule> {
    public TemplateModule Get() {
      var builder = new TemplateModule.Builder();
      builder.AddDecorator<AdjustableWaterOutput, AdjustableWaterOutputMarker>();
      return builder.Build();
    }
  }
}
