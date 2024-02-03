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
namespace IgorZ.TimberCommons.IrrigationSystemUI {

/// <summary>Harmony patch to improve consumption rate formatting in the building description tooltip.</summary>
/// <remarks>
/// Instead of doing own logic on the formatting, it let's the components decide via
/// <see cref="IConsumptionRateFormatter"/>.
/// </remarks>
[HarmonyPatch(typeof(GoodConsumingBuildingDescriber), nameof(GoodConsumingBuildingDescriber.DescribeSupply))]
static class GoodConsumingBuildingDescriberPatch {
  // ReSharper disable once UnusedMember.Local
  static void Postfix(ref bool __runOriginal, ref EntityDescription __result,
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
