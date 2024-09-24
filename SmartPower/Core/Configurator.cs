// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using HarmonyLib;
using IgorZ.TimberDev.Utils;
using Timberborn.MechanicalSystem;
using Timberborn.PowerGenerating;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace IgorZ.SmartPower.Core {

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly PrefabPatcher.RequiredComponentsDep PoweredAttractionDeps =
      new(AccessTools.TypeByName("Timberborn.Attractions.AttractionSpec"), typeof(MechanicalBuilding));
  static readonly PrefabPatcher.RequiredComponentsDep ManufactoryDeps =
      new(AccessTools.TypeByName("Timberborn.Workshops.ManufactorySpec"));
  static readonly PrefabPatcher.RequiredComponentsDep WalkerPoweredGeneratorDeps =
      new(AccessTools.TypeByName("Timberborn.PowerGenerating.WalkerPoweredGenerator"));
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    if (Features.DebugExVerboseLogging && DebugEx.LoggingSettings.VerbosityLevel < 5) {
      DebugEx.LoggingSettings.VerbosityLevel = 5;
    }
    HarmonyPatcher.PatchRepeated(
        PatchId + "-core",
        typeof(MechanicalBuildingPatch), typeof(WalkerPoweredGeneratorPatch));

    CustomizableInstantiator.AddPatcher(PatchId + "-instantiator", PatchMethod);
  }

  static void PatchMethod(GameObject prefab) {
    PrefabPatcher.ReplaceComponent<GoodPoweredGenerator, SmartGoodPoweredGenerator>(prefab);
    PrefabPatcher.AddComponent<SmartManufactory>(prefab, ManufactoryDeps.Check);
    PrefabPatcher.AddComponent<SmartPoweredAttraction>(prefab, PoweredAttractionDeps.Check);

    PrefabPatcher.AddComponent<PowerOutputBalancer>(
        prefab, WalkerPoweredGeneratorDeps.Check, onAdd: balancer => {
          balancer.runWhenPaused = true;
          balancer.waitTicks = 3;
        });
    PrefabPatcher.AddComponent<AutoPausePowerGenerator>(prefab, WalkerPoweredGeneratorDeps.Check);
  }
}

}
