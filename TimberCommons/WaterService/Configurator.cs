// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.TimberCommons.WaterService;

[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;
  static readonly Type[] Patches = [
      typeof(SoilMoistureSimulatorPatch),
      typeof(SoilMoistureMapPatch)
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(PatchId, Patches);
    DirectSoilMoistureSystemAccessor.ResetStaticState();
    containerDefinition.Bind<DirectSoilMoistureSystemAccessor>().AsSingleton();
  }
}
