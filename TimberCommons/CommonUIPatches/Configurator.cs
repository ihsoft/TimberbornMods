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
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;
  static readonly Type[] Patches = [
      typeof(GoodConsumingBuildingDescriberPatch),
      typeof(ManufactoryInventoryFragmentPatch1),
      typeof(ManufactoryInventoryFragmentPatch2),
      typeof(SluiceFragmentPatch1),
      typeof(SluiceFragmentPatch2),
      typeof(GrowableToolPanelItemFactoryPatch),
      typeof(GrowableFragmentPatch),
      typeof(ManufactoryDescriberPatch1),
      typeof(ManufactoryDescriberPatch2)
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    CommonFormats.ResetCachedLocStrings();
    HarmonyPatcher.ApplyPatch(PatchId, Patches);
  }
}