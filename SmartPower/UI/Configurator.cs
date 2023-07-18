// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils.Utils;
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
    HarmonyPatcher.PatchRepeated(
        PatchId,
        typeof(NetworkFragmentServicePatch),
        typeof(ConsumerFragmentServicePatch));
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
