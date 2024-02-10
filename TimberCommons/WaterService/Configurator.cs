// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Logging;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;

namespace IgorZ.TimberCommons.WaterService {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(
        GetType().AssemblyQualifiedName,
        typeof(TerrainMaterialMapSetDesertIntensityPatch),
        typeof(SoilMoistureSimulatorGetUpdatedMoisturePatch));

    containerDefinition.Bind<DirectWaterServiceAccessor>().AsSingleton();
    containerDefinition.Bind<DirectSoilMoistureSystemAccessor>().AsSingleton();
    containerDefinition.Bind<ThreadedLogsRecorder>().AsSingleton();
  }
}

}
