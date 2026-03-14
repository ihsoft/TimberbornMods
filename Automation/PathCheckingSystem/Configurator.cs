// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.Automation.PathCheckingSystem;

// ReSharper disable once UnusedType.Global
[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<PathCheckingService>().AsSingleton();
    containerDefinition.Bind<PathCheckingSite>().AsTransient();

    HarmonyPatcher.ApplyPatch(PatchId, typeof(ConstructionSiteFinishIfRequirementsMetPatch));
  }
}