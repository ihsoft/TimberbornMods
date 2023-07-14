// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using Bindito.Core;
using HarmonyLib;
using IgorZ.TimberDev.CustomInstantiator;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.Attractions;
using Timberborn.EnterableSystem;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.PowerGenerating;
using IgorZ.TimberDev.Utils.Utils;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly PrefabPatcher.RequiredComponentsDep SmartAttractionDeps =
      new(typeof(Enterable), typeof(Attraction), typeof(MechanicalBuilding));

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(typeof(Configurator).FullName, typeof(NetworkFragmentServicePatch));
    CustomizableInstantiator.AddPatcher(
        typeof(Configurator).FullName,
        prefab => {
          PrefabPatcher.ReplaceComponent<GoodPoweredGenerator, SmartGoodPoweredGenerator>(prefab, _ => true);
          PrefabPatcher.AddComponent<SmartPoweredAttraction>(prefab, SmartAttractionDeps.Check);
        });
  }

  [HarmonyPatch]
  static class NetworkFragmentServicePatch {
    const string NetworkFragmentServiceClassName = "Timberborn.MechanicalSystemUI.NetworkFragmentService";
    const string MethodName = "Update";

    static MethodBase TargetMethod() {
      var type = AccessTools.TypeByName(NetworkFragmentServiceClassName);
      return AccessTools.FirstMethod(type, method => method.Name == MethodName);
    }

    static void Postfix(MechanicalNode mechanicalNode, Label ____label, ILoc ____loc) {
      var text = StateTextFormatter.FormatBatteryText(mechanicalNode, ____loc);
      if (text != "") {
        ____label.text += "\n" + text;
      }
    }
  }
}

}
