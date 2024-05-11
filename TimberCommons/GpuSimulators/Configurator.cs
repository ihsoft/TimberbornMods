// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.EntityPanelSystem;

namespace IgorZ.TimberCommons.GpuSimulators {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    // HarmonyPatcher.PatchRepeated(
    //     GetType().AssemblyQualifiedName,
    //     typeof(SoilContaminationSimulatorTickSimulationPatch),
    //     typeof(SoilMoistureSimulatorTickSimulationPatch),
    //     typeof(WaterSimulatorTickSimulationPatch));
    //
    // containerDefinition.Bind<GpuSimulatorsController>().AsSingleton();
    // containerDefinition.Bind<GpuSoilContaminationSimulator>().AsSingleton();
    // containerDefinition.Bind<GpuSoilMoistureSimulator>().AsSingleton();
    // containerDefinition.Bind<GpuWaterSimulator>().AsSingleton();
    // containerDefinition.Bind<GpuSimulatorsDebuggingPanel>().AsSingleton();
    //
    // containerDefinition.Bind<DebugUiFragment>().AsSingleton();
    // containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
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
