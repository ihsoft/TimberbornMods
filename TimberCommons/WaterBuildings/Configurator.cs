﻿// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.TemplateSystem;

namespace IgorZ.TimberCommons.WaterBuildings;

[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.MultiBind<TemplateModule>().ToProvider<WaterOutputTemplateModuleProvider>().AsSingleton();
    HarmonyPatcher.PatchRepeated(PatchId, typeof(WaterOutputPatch), typeof(DecoratorDefinitionPatch));
  }

  class WaterOutputTemplateModuleProvider : IProvider<TemplateModule> {
    public TemplateModule Get() {
      var builder = new TemplateModule.Builder();
      builder.AddDecorator<AdjustableWaterOutput, AdjustableWaterOutputMarker>();
      return builder.Build();
    }
  }
}
