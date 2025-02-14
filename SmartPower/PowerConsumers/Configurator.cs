// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.Attractions;
using Timberborn.MechanicalSystem;
using Timberborn.Workshops;
using UnityEngine;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
namespace IgorZ.SmartPower.PowerConsumers;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly PrefabPatcher.RequiredComponentsDep PoweredManufactoryDep =
      new(typeof(MechanicalBuildingSpec), typeof(ManufactorySpec));
  static readonly PrefabPatcher.RequiredComponentsDep PoweredAttractionDep =
      new(typeof(MechanicalBuildingSpec), typeof(AttractionSpec));
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    CustomizableInstantiator.AddPatcher(PatchId + "-instantiator", PatchMethod);
  }

  static void PatchMethod(GameObject prefab) {
    PrefabPatcher.AddComponent<SmartManufactory>(prefab, PoweredManufactoryDep.Check);
    PrefabPatcher.AddComponent<SmartPoweredAttraction>(prefab, PoweredAttractionDep.Check);
    PrefabPatcher.AddComponent<PowerInputLimiter>(prefab, o => {
      var node = o.GetComponent<MechanicalNodeSpec>();
      return node && node.PowerInput > 0 && node.PowerOutput == 0;
    });
  }
}
