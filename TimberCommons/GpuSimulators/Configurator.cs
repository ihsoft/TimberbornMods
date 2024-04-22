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
        GetType().AssemblyQualifiedName, typeof(SoilContaminationSimulatorTickSimulationPatch));

    containerDefinition.Bind<GpuSoilContaminationSimulator>().AsSingleton();
    containerDefinition.Bind<GpuSoilContaminationSimulator2>().AsSingleton();
    containerDefinition.Bind<GpuSimulatorsDebuggingPanel>().AsSingleton();
  }
}

}
