// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.TimberCommons.WaterService;

[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;
  static readonly Type[] Patches = [
      typeof(SoilMoistureSimulatorPatch),
      typeof(SoilMoistureServicePatch),
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.ApplyPatch(PatchId, Patches);
    containerDefinition.Bind<SoilOverridesService>().AsSingleton();
  }
}
