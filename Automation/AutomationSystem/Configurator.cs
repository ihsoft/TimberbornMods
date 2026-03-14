// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.BlueprintSystem;
using Timberborn.Buildings;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.AutomationSystem;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<AutomationService>().AsSingleton();
    containerDefinition.Bind<AutomationBehavior>().AsTransient();
    CustomizableInstantiator.AddPatcher(PatchId + "-instantiator", PatchMethod);
  }

  static void PatchMethod(Blueprint blueprint, List<object> components) {
    if (blueprint.Name.StartsWith("Path") || !blueprint.HasSpec<BuildingSpec>()) {
      return;
    }
    components.Add(StaticBindings.DependencyContainer.GetInstance<AutomationBehavior>());
  }
}
