// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.CustomInstantiator;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.Attractions;
using Timberborn.EnterableSystem;
using Timberborn.MechanicalSystem;
using Timberborn.PowerGenerating;
using IgorZ.TimberDev.Utils;
using Timberborn.Workshops;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly PrefabPatcher.RequiredComponentsDep SmartAttractionDeps =
      new(typeof(Enterable), typeof(Attraction), typeof(MechanicalBuilding));
  static readonly PrefabPatcher.RequiredComponentsDep SmartMechBuildingDeps =
      new(typeof(Manufactory), typeof(MechanicalBuilding));
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    CustomizableInstantiator.AddPatcher(
        PatchId,
        prefab => {
          PrefabPatcher.ReplaceComponent<GoodPoweredGenerator, SmartGoodPoweredGenerator>(prefab, _ => true);
          PrefabPatcher.ReplaceComponent<MechanicalBuilding, SmartMechanicalBuilding>(
              prefab, SmartMechBuildingDeps.Check);
          PrefabPatcher.AddComponent<SmartPoweredAttraction>(prefab, SmartAttractionDeps.Check);
        });
  }
}

}
