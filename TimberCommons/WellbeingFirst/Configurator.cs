// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.Beavers;
using Timberborn.EntityPanelSystem;
using Timberborn.GameDistricts;
using Timberborn.TemplateSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.TimberCommons.WellbeingFirst {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<DebugUiFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();

    var patches = new List<Type> {
        // typeof(GetBestNeedBehaviorPatch),
        // typeof(GetBestNeedBehaviorPatch2),
        typeof(DistrictNeedBehaviorServicePatch),
    };
    DebugEx.Warning("*** patched for test!!!!");
    HarmonyPatcher.PatchRepeated(PatchId, patches.ToArray());
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<Citizen, HaulerWellbeingOptimizer>();
    return builder.Build();
  }

  sealed class EntityPanelModuleProvider : IProvider<EntityPanelModule> {
    readonly DebugUiFragment _fragment;

    public EntityPanelModuleProvider(DebugUiFragment fragment) {
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
