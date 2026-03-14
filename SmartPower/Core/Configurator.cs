// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace IgorZ.SmartPower.Core;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<SmartPowerService>().AsSingleton();
  }
}
