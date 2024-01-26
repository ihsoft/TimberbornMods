// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using IgorZ.TimberCommons.Common;
using Timberborn.EntityPanelSystem;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.GoodConsumingBuildingSystemUI;
using Timberborn.GoodsUI;
using Timberborn.Localization;
using Timberborn.UIFormatters;

namespace IgorZ.TimberCommons.IrrigationSystemUI {

/// <summary>Harmony patch to improve consumption rate formatting.</summary>
/// <remarks>
/// Instead of doing own logic on the formatting, it let's teh components decide via
/// <see cref="IConsumptionRateFormatter"/>.
/// </remarks>
[HarmonyPatch(typeof(GoodConsumingBuildingDescriber), nameof(GoodConsumingBuildingDescriber.DescribeSupply))]
static class GoodConsumingBuildingDescriberPatch {
  const string DescriptionLocKey = "GoodConsuming.SupplyDescription";

  [SuppressMessage("ReSharper", "InconsistentNaming")]
  // ReSharper disable once UnusedMember.Local
  static void Postfix(ref bool __runOriginal, ref EntityDescription __result,
                      ILoc ____loc, GoodDescriber ____goodDescriber,
                      DescribedAmountFactory ____describedAmountFactory,
                      ResourceAmountFormatter ____resourceAmountFormatter,
                      ProductionItemFactory ____productionItemFactory,
                      GoodConsumingBuilding ____goodConsumingBuilding) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }

    var formatters = new List<IConsumptionRateFormatter>();
    ____goodConsumingBuilding.GetComponentsFast(formatters);
    if (formatters.Count > 0) {
      var formatter = formatters[0];
      var describedGood = ____goodDescriber.GetDescribedGood(____goodConsumingBuilding.Supply);
      var param = ____resourceAmountFormatter.FormatPerHour(
          describedGood.DisplayName, ____goodConsumingBuilding.GoodPerHour);
      var tooltip = ____loc.T(DescriptionLocKey, param);
      var input = ____describedAmountFactory.CreatePlain("", formatter.GetRate(), describedGood.Icon, tooltip);
      var content = ____productionItemFactory.CreateInput(input);
      __result = EntityDescription.CreateInputSectionWithTime(content, int.MaxValue, formatter.GetTime());
    }
  }
}

}
