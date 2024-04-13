// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;

namespace Automation.PathCheckingSystem {

// ReSharper disable once UnusedType.Global
[Configurator(SceneEntrypoint.InGame)]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<PathCheckingService>().AsSingleton();

    HarmonyPatcher.PatchRepeated(PatchId, typeof(ConstructionSiteFinishIfRequirementsMetPatch));
  }
}

}
