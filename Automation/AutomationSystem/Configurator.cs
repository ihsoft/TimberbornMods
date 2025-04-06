// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.Buildings;
using Timberborn.PrefabSystem;
using UnityEngine;

namespace IgorZ.Automation.AutomationSystem;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<AutomationService>().AsSingleton();
    CustomizableInstantiator.AddPatcher(PatchId + "-instantiator", PatchMethod);
  }

  static void PatchMethod(GameObject prefab) {
    PrefabPatcher.AddComponent<AutomationBehavior>(prefab, obj => {
      if (!obj.GetComponent<BuildingSpec>()) {
        return false;
      }
      var prefabSpec = obj.GetComponent<PrefabSpec>();
      return !prefabSpec.Name.StartsWith("Path.");
    });
  }
}
