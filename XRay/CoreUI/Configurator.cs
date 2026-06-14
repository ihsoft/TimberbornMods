// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.XRay.CoreUI;

[Context("Game")]
[Context("MapEditor")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<XRayModeTogglePanel>().AsSingleton();
  }
}