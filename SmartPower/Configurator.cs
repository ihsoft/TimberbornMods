// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Bindito.Unity;
using HarmonyLib;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.Attractions;
using Timberborn.EnterableSystem;
using Timberborn.MechanicalSystem;
using Timberborn.PowerGenerating;
using UnityEngine;

namespace SmartPower {

[Configurator(SceneEntrypoint.MainMenu)]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    var harmony = new Harmony("IgorZ.SmartPower");
    harmony.PatchAll(typeof(PrefabInstantiatorPatch));
  }
}

[HarmonyPatch(typeof(Instantiator), nameof(Instantiator.InstantiateInactive))]
public static class PrefabInstantiatorPatch {
  static readonly PrefabPatcher.RequiredComponentsDep SmartAttractionDeps =
      new(typeof(Enterable), typeof(Attraction), typeof(MechanicalBuilding));

  static bool Prefix(GameObject prefab) {
    PrefabPatcher.ReplaceComponent<GoodPoweredGenerator, SmartGoodPoweredGenerator>(prefab, _ => true);
    PrefabPatcher.AddComponent<SmartPoweredAttraction>(prefab, SmartAttractionDeps.Check);
    return true;
  }
}

}
