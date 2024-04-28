// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;

namespace IgorZ.TimberCommons.MultiThreadSimulators {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(
        GetType().AssemblyQualifiedName,
        typeof(ParallelWaterSimulatorPatch),
        typeof(ParallelSoilMoistureSimulatorPatch),
        typeof(ParallelSoilContaminationSimulatorPatch));
    ParallelWaterSimulatorPatch.Initialize();
    ParallelSoilMoistureSimulatorPatch.Initialize();
    ParallelSoilContaminationSimulatorPatch.Initialize();
  }
}

}
