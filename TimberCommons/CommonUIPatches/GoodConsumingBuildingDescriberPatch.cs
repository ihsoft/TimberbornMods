// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using IgorZ.TimberCommons.Common;
using Timberborn.BaseComponentSystem;
using Timberborn.EntityPanelSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches;

/// <summary>Harmony patch to support <see cref="IConsumptionRateFormatter"/> on buildings.</summary>
/// <remarks>
/// Instead of doing own logic on the formatting, it lets the components decide via
/// <see cref="IConsumptionRateFormatter"/>.
/// </remarks>
[HarmonyPatch]
static class GoodConsumingBuildingDescriberPatch {
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod(
      "Timberborn.GoodConsumingBuildingSystemUI.GoodConsumingBuildingDescriber:DescribeSupply");
  }

  static void Postfix(bool __runOriginal, ref EntityDescription __result, BaseComponent ____goodConsumingBuilding) {
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