// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;

namespace IgorZ.TimberCommons.CommonUIPatches;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class ConfiguratorForGame : IConfigurator {
  static readonly string PatchId = typeof(ConfiguratorForGame).AssemblyQualifiedName;
  static readonly Type[] Patches = [
      typeof(GoodConsumingBuildingDescriberPatch),
      typeof(ManufactoryInventoryFragmentPatch1),
      typeof(ManufactoryInventoryFragmentPatch2),
      typeof(GrowableToolPanelItemFactoryPatch),
      typeof(GrowableFragmentPatch),
      typeof(ManufactoryDescriberPatch1),
      typeof(ManufactoryDescriberPatch2),
      typeof(ModListViewInitializePatch),
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    CommonFormats.ResetCachedLocStrings();
    HarmonyPatcher.ApplyPatch(PatchId, Patches);
    containerDefinition.Bind<ModListViewLocInitializer>().AsSingleton();
  }
}
