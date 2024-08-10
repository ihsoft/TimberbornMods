// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.EntityPanelSystem;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower.UI {

[Context("Game")]
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
    readonly SmartGoodPoweredGeneratorFragment _goodPoweredGeneratorFragment;

    public EntityPanelModuleProvider(SmartGoodPoweredGeneratorFragment goodPoweredGeneratorFragment) {
      _goodPoweredGeneratorFragment = goodPoweredGeneratorFragment;
    }

    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddMiddleFragment(_goodPoweredGeneratorFragment);
      return builder.Build();
    }
  }
}

}
