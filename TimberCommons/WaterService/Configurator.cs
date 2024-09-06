// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.Utils;

namespace IgorZ.TimberCommons.WaterService {

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    var patches = new List<Type> { typeof(SoilMoistureSimulatorPatch) };
    if (Features.OverrideDesertLevelsForWaterTowers) {
      patches.Add(typeof(SoilMoistureMapPatch));
    }
    HarmonyPatcher.PatchRepeated(GetType().AssemblyQualifiedName, patches.ToArray());

    DirectSoilMoistureSystemAccessor.ResetStaticState();
    containerDefinition.Bind<DirectSoilMoistureSystemAccessor>().AsSingleton();
  }
}

}
