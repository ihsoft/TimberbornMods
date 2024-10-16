// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using HarmonyLib;
using IgorZ.TimberDev.Utils;
using Timberborn.MechanicalSystem;
using UnityEngine;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
namespace IgorZ.SmartPower.PowerConsumers;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly PrefabPatcher.RequiredComponentsDep PoweredManufactoryDep =
      new(typeof(MechanicalBuilding), AccessTools.TypeByName("Timberborn.Workshops.ManufactorySpec"));
  static readonly PrefabPatcher.RequiredComponentsDep PoweredAttractionDep =
      new(typeof(MechanicalBuilding), AccessTools.TypeByName("Timberborn.Attractions.AttractionSpec"));
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    CustomizableInstantiator.AddPatcher(PatchId + "-instantiator", PatchMethod);
  }

  static void PatchMethod(GameObject prefab) {
    PrefabPatcher.AddComponent<PowerInputLimiter>(prefab, PoweredManufactoryDep.Check);
    PrefabPatcher.AddComponent<PowerInputLimiter>(prefab, PoweredAttractionDep.Check);
  }
}
