// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using HarmonyLib;
using IgorZ.SmartPower.Core;
using Timberborn.Attractions;
using Timberborn.MechanicalSystem;
using Timberborn.PowerGenerating;
using IgorZ.TimberDev.Utils;
using Timberborn.Workshops;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly PrefabPatcher.RequiredComponentsDep PoweredAttractionDeps =
      new(typeof(Attraction), typeof(MechanicalBuilding));
  static readonly PrefabPatcher.RequiredComponentsDep ManufactoryDeps =
      new(typeof(Manufactory));
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
        prefab, WalkerPoweredGeneratorDeps.Check, balancer => {
          balancer.runWhenPaused = true;
          balancer.waitTicks = 3;
        });
    PrefabPatcher.AddComponent<AutoPausePowerGenerator>(prefab, WalkerPoweredGeneratorDeps.Check);
  }
}

}
