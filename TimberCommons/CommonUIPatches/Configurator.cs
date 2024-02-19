// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.Utils;
using TimberApi.SceneSystem;
using TimberApi.ConfiguratorSystem;

namespace IgorZ.TimberCommons.CommonUIPatches {

// ReSharper disable once UnusedType.Global
[Configurator(SceneEntrypoint.InGame)]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    if (Features.DisableAllUiPatches) {
      return;
    }
    var patches = new List<Type> {
        typeof(GoodConsumingBuildingDescriberPatch),
        typeof(ManufactoryInventoryFragmentInitializeFragmentPatch),
        typeof(ManufactoryInventoryFragmentUpdateFragmentPatch),
    };
    if (Features.GoodConsumingBuildingUIDaysHoursForAll) {
      patches.Add(typeof(GoodConsumingBuildingFragmentPatch));
    }
    if (Features.GrowableGrowthTimeUIDaysHoursViewForAll) {
      patches.Add(typeof(GrowableToolPanelItemFactoryPatch));
      patches.Add(typeof(GrowableFragmentPatch));
    }
    if (Features.ShowDaysHoursForSlowRecipes) {
      patches.Add(typeof(ManufactoryDescriberGetCraftingTimePatch));
    }
    if (Features.ShowLongValueForLowFuelConsumptionRecipes) {
      patches.Add(typeof(ManufactoryDescriberGetInputsPatch));
    }
    HarmonyPatcher.PatchRepeated(PatchId, patches.ToArray());
  }
}

}
