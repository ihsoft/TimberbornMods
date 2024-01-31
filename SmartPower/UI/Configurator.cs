// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using Bindito.Core;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.EntityPanelSystem;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower.UI {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    var patches = new List<Type> { typeof(ConsumerFragmentServicePatch) };
    if (Features.NetworkShowBatteryStats) {
      patches.Add(typeof(NetworkFragmentServicePatch));
    }
    HarmonyPatcher.PatchRepeated(PatchId, patches.ToArray());
    containerDefinition.Bind<SmartGoodPoweredGeneratorFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }

  sealed class EntityPanelModuleProvider : IProvider<EntityPanelModule> {
    readonly SmartGoodPoweredGeneratorFragment _automationFragment;

    public EntityPanelModuleProvider(SmartGoodPoweredGeneratorFragment automationFragment) {
      _automationFragment = automationFragment;
    }

    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(_automationFragment);
      return builder.Build();
    }
  }
}

}
