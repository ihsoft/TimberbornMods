// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.XRay.Core;

[Context("Game")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<KeyBindingInputProcessor>().AsSingleton();
    containerDefinition.Bind<XRayService>().AsSingleton();
    containerDefinition.Bind<RendererFactory>().AsSingleton();
    containerDefinition.Bind<TerrainRayCaster>().AsSingleton();
  }
}