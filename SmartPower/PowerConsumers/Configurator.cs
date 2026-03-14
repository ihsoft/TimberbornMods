// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.Attractions;
using Timberborn.BlueprintSystem;
using Timberborn.MechanicalSystem;
using Timberborn.Workshops;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
namespace IgorZ.SmartPower.PowerConsumers;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<PowerInputLimiter>().AsTransient();
    containerDefinition.Bind<SmartPoweredAttraction>().AsTransient();
    containerDefinition.Bind<SmartManufactory>().AsTransient();
    CustomizableInstantiator.AddPatcher(PatchId + "-instantiator", PatchMethod);
  }

  static void PatchMethod(Blueprint blueprint, List<object> components) {
    var mechanicalNodeSpec = blueprint.GetSpec<MechanicalNodeSpec>();
    if (mechanicalNodeSpec == null) {
      return;
    }
    if (mechanicalNodeSpec.PowerInput > 0 && mechanicalNodeSpec.PowerOutput == 0) {
      components.Add(StaticBindings.DependencyContainer.GetInstance<PowerInputLimiter>());
    }
    if (blueprint.HasSpec<AttractionSpec>()) {
      components.Add(StaticBindings.DependencyContainer.GetInstance<SmartPoweredAttraction>());
    } else if (blueprint.HasSpec<ManufactorySpec>()) {
      components.Add(StaticBindings.DependencyContainer.GetInstance<SmartManufactory>());
    }
  }
}
