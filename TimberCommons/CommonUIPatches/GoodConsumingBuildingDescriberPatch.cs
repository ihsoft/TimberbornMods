// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberCommons.Common;
using Timberborn.EntityPanelSystem;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.GoodConsumingBuildingSystemUI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.CommonUIPatches {

/// <summary>Harmony patch to support <see cref="IConsumptionRateFormatter"/> on buildings.</summary>
/// <remarks>
/// Instead of doing own logic on the formatting, it let's the components decide via
/// <see cref="IConsumptionRateFormatter"/>.
/// </remarks>
[HarmonyPatch(typeof(GoodConsumingBuildingDescriber), nameof(GoodConsumingBuildingDescriber.DescribeSupply))]
static class GoodConsumingBuildingDescriberPatch {
  // ReSharper disable once UnusedMember.Local
  static void Postfix(bool __runOriginal, ref EntityDescription __result,
                      GoodConsumingBuilding ____goodConsumingBuilding) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    var formatter = ____goodConsumingBuilding.GetComponentFast<IConsumptionRateFormatter>();
    if (formatter == null) {
      return;
    }
    var fuelAmountLabel = __result.Section.Q<Label>("Amount");
    if (fuelAmountLabel != null) {
      fuelAmountLabel.text = formatter.GetRate();
      __result = EntityDescription.CreateInputSectionWithTime(__result.Section, int.MaxValue, formatter.GetTime());
    } else {
      DebugEx.Warning("Cannot override GoodConsumingBuilding description");
    }
  }
}

}
