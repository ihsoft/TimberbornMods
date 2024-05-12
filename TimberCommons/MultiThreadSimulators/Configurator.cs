// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.EntityPanelSystem;

namespace IgorZ.TimberCommons.MultiThreadSimulators {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    if (!Features.ShowMultiThreadedSimulatorsPanel) {
      return;
    }

    HarmonyPatcher.PatchRepeated(
        GetType().AssemblyQualifiedName,
        typeof(ParallelWaterSimulatorPatch),
        typeof(ParallelSoilMoistureSimulatorPatch),
        typeof(ParallelSoilContaminationSimulatorPatch));
    ParallelWaterSimulatorPatch.Initialize();
    ParallelSoilMoistureSimulatorPatch.Initialize();
    ParallelSoilContaminationSimulatorPatch.Initialize();

    containerDefinition.Bind<DebugUiFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
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
