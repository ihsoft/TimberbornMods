// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.Growing;
using Timberborn.TemplateSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem {

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    CustomizableInstantiator.AddPatcher(PatchId + "-IrrigationSystem-instantiator", PatchMethod);
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<Growable, GrowthRateModifier>();
    return builder.Build();
  }

  static void PatchMethod(GameObject obj) {
    PrefabPatcher.AddComponent<GoodConsumingIrrigationTower>(
        obj,
        prefab => prefab.name.StartsWith("IrrigationTower."),
        instance => {
          instance._irrigationRange = 10;
        });
  }
}

}
