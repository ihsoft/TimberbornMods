﻿using Bindito.Core;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.EntityPanelSystem;

namespace IgorZ.TimberCommons.IrrigationSystemUI {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<IrrigationTowerFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
    HarmonyPatcher.PatchRepeated(
        PatchId, typeof(GoodConsumingBuildingFragmentPatch), typeof(GoodConsumingBuildingDescriberPatch));
  }

  /// <summary>UI for the irrigation tower component.</summary>
  sealed class EntityPanelModuleProvider : IProvider<EntityPanelModule> {
    readonly IrrigationTowerFragment _irrigationTowerFragment;

    public EntityPanelModuleProvider(IrrigationTowerFragment irrigationTowerFragment) {
      _irrigationTowerFragment = irrigationTowerFragment;
    }

    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(_irrigationTowerFragment);
      return builder.Build();
    }
  }
}

}