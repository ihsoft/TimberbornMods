// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;

namespace IgorZ.TimberCommons.CommonUIPatches {

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    if (Features.DisableAllUiPatches) {
      return;
    }
    var patches = new List<Type> {
        typeof(GoodConsumingBuildingDescriberPatch),
        typeof(ManufactoryInventoryFragmentPatch1),
        typeof(ManufactoryInventoryFragmentPatch2),
        typeof(GoodConsumingBuildingFragmentPatch),
        typeof(SluiceFragmentPatch1),
        typeof(SluiceFragmentPatch2),
    };
    CommonFormats.ResetCachedLocStrings();
    if (Features.GrowableGrowthTimeUIDaysHoursViewForAll) {
      patches.Add(typeof(GrowableToolPanelItemFactoryPatch));
      patches.Add(typeof(GrowableFragmentPatch));
    }
    if (Features.ShowDaysHoursForSlowRecipes) {
      patches.Add(typeof(ManufactoryDescriberPatch1));
    }
    if (Features.ShowLongValueForLowFuelConsumptionRecipes) {
      patches.Add(typeof(ManufactoryDescriberPatch2));
    }
    HarmonyPatcher.PatchRepeated(PatchId, patches.ToArray());
  }
}

}
