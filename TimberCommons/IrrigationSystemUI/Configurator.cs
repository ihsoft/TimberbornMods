// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.EntityPanelSystem;

namespace IgorZ.TimberCommons.IrrigationSystemUI;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;
  static readonly Type[] Patches = [
      typeof(GoodConsumingBuildingFragmentPatch),
  ];

  /// <inheritdoc/>
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<IrrigationTowerFragment>().AsSingleton();
    containerDefinition.Bind<GrowthRateModifierFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
    HarmonyPatcher.ApplyPatch(PatchId, Patches);
  }

  /// <summary>UI for the irrigation related components.</summary>
  /// <remarks>The tower and boosted trees.</remarks>
  sealed class EntityPanelModuleProvider(
      IrrigationTowerFragment irrigationTowerFragment,
      GrowthRateModifierFragment growthRateModifierFragment) : IProvider<EntityPanelModule> {

    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(irrigationTowerFragment);
      builder.AddBottomFragment(growthRateModifierFragment);
      return builder.Build();
    }
  }
}