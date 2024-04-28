// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;

namespace IgorZ.TimberCommons.GpuSimulators {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(
        GetType().AssemblyQualifiedName,
        typeof(SoilContaminationSimulatorTickSimulationPatch),
        typeof(SoilMoistureSimulatorTickSimulationPatch),
        typeof(WaterSimulatorTickSimulationPatch));

    containerDefinition.Bind<GpuSimulatorsController>().AsSingleton();
    containerDefinition.Bind<GpuSoilContaminationSimulator>().AsSingleton();
    containerDefinition.Bind<GpuSoilMoistureSimulator>().AsSingleton();
    containerDefinition.Bind<GpuWaterSimulator>().AsSingleton();
    containerDefinition.Bind<GpuSimulatorsDebuggingPanel>().AsSingleton();
  }
}

}
