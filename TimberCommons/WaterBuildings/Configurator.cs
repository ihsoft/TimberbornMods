// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.Utils;
using Timberborn.TemplateSystem;
using Timberborn.WaterBuildings;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterBuildings;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly PrefabPatcher.RequiredComponentsDep AdjustableWaterOutputDeps =
      new(typeof(AdjustableWaterOutput));

  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    if (!Features.AdjustWaterOutputWaterDepthAtSpillway) {
      return;
    }
    containerDefinition.MultiBind<TemplateModule>().ToProvider<AttractionTemplateModuleProvider>().AsSingleton();
    HarmonyPatcher.PatchRepeated(PatchId, [typeof(WaterOutputPatch)]);
    CustomizableInstantiator.AddPatcher(PatchId + "-instantiator", PatchMethod);
  }

  static void PatchMethod(GameObject obj) {
    PrefabPatcher.ReplaceComponent<WaterOutput, AdjustableWaterOutput>(
        obj, onReplace: (stockComponent, newComponent) => {
          newComponent._waterCoordinates = stockComponent._waterCoordinates;
        });
    //PrefabPatcher.AddComponent<AdjustableWaterOutputMarker>(obj, AdjustableWaterOutputDeps.Check); 
  }

  class AttractionTemplateModuleProvider : IProvider<TemplateModule> {
    public TemplateModule Get()
    {
      TemplateModule.Builder builder = new TemplateModule.Builder();
      builder.AddDecorator<AdjustableWaterOutput, AdjustableWaterOutputMarker>();
      return builder.Build();
    }
  }
}
