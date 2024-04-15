// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.EntityPanelSystem;

namespace IgorZ.TimberCommons.WaterService {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    var patches = new List<Type> { typeof(SoilMoistureSimulatorGetUpdatedMoisturePatch) };
    if (Features.OverrideDesertLevelsForWaterTowers) {
      patches.Add(typeof(SoilMoistureMapUpdateDesertIntensityPatch));
    }
    HarmonyPatcher.PatchRepeated(GetType().AssemblyQualifiedName, patches.ToArray());

    containerDefinition.Bind<DirectWaterServiceAccessor>().AsSingleton();
    DirectSoilMoistureSystemAccessor.ResetStaticState();
    containerDefinition.Bind<DirectSoilMoistureSystemAccessor>().AsSingleton();
    containerDefinition.Bind<WaterSourceFragmentDebug>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }

  sealed class EntityPanelModuleProvider : IProvider<EntityPanelModule> {
    readonly WaterSourceFragmentDebug _fragment;

    public EntityPanelModuleProvider(WaterSourceFragmentDebug fragment) {
      _fragment = fragment;
    }

    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(_fragment);
      return builder.Build();
    }
  }
}

}
