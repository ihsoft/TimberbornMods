// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.EntityPanelSystem;

namespace IgorZ.TimberCommons.WaterBuildingsUI;

[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;
  static readonly Type[] Patches = [
      typeof(ThrottlingValveFragmentPatch),
  ];

  /// <inheritdoc/>
  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.ApplyPatch(PatchId, Patches);
    containerDefinition.Bind<AdjustableWaterOutputFragment>().AsSingleton();
    containerDefinition.Bind<ThrottlingValveFragmentLocInitializer>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }

  sealed class EntityPanelModuleProvider : IProvider<EntityPanelModule> {
    readonly AdjustableWaterOutputFragment _adjustableWaterOutputFragment;

    EntityPanelModuleProvider(AdjustableWaterOutputFragment adjustableWaterOutputFragment) {
     _adjustableWaterOutputFragment = adjustableWaterOutputFragment;
    }

    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(_adjustableWaterOutputFragment);
      return builder.Build();
    }
  }
}
