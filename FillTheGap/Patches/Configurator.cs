// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.FillTheGap.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.SingletonSystem;

namespace IgorZ.FillTheGap.Patches;

[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<PatchServiceInitializer>().AsSingleton();
    HarmonyPatcher.ApplyPatch(
        PatchId,
        typeof(BlockObjectPatch),
        typeof(TerrainAndBlockObjectsToDeleteFinderPatch),
        typeof(TerraformingDirectionalBlockerPatch));
  }

  sealed class PatchServiceInitializer : ILoadableSingleton {
    public PatchServiceInitializer(PlatformReplacementService platformReplacementService) {
      BlockObjectPatch.SetService(platformReplacementService);
      TerrainAndBlockObjectsToDeleteFinderPatch.SetService(platformReplacementService);
      TerraformingDirectionalBlockerPatch.SetService(platformReplacementService);
    }

    public void Load() {
    }
  }
}
