// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.XRay.Settings;

[Context("MainMenu")]
[Context("Game")]
[Context("MapEditor")]
sealed class Configurator : IConfigurator {
  public const string AutomationModId = "Timberborn.IgorZ.XRay";

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<MeshSettings>().AsSingleton();
  }
}
