// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using Bindito.Core;
using Bindito.Unity;
using HarmonyLib;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.Attractions;
using Timberborn.EnterableSystem;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.PowerGenerating;
using TimberDev.Utils;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
namespace SmartPower {

[Configurator(SceneEntrypoint.MainMenu)]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    var harmony = new Harmony("IgorZ.SmartPower");
    harmony.PatchAll(typeof(PrefabInstantiatorPatch));
    harmony.PatchAll(typeof(NetworkFragmentServicePatch));
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

[HarmonyPatch]
public static class NetworkFragmentServicePatch {
  const string NetworkFragmentServiceClassName = "Timberborn.MechanicalSystemUI.NetworkFragmentService";
  const string MethodName = "Update";

  static MethodBase TargetMethod() {
    var type = AccessTools.TypeByName(NetworkFragmentServiceClassName);
    return AccessTools.FirstMethod(type, method => method.Name == MethodName);
  }

  static void Postfix(MechanicalNode mechanicalNode, Label ____label, ILoc ____loc) {
    var text = BatteryStateTextFormatter.FormatBatteryText(mechanicalNode, ____loc);
    if (text != "") {
      ____label.text += "\n" + text;
    }
  }
}

}
