// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using IgorZ.TimberDev.UI;
using Timberborn.BlockSystem;
using Timberborn.Localization;
using Timberborn.WaterBuildings;
using UnityEngine;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches {

[HarmonyPatch]
static class SluiceFragmentPatch2 {
  const string WaterCurrentLocKey = "Building.StreamGauge.Current"; 

  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.WaterBuildingsUI.SluiceFragment:UpdateFragment");
  }

  static void Postfix(bool __runOriginal, Sluice ____sluice, ILoc ____loc) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (!____sluice) {
      return;
    }

    if (SluiceFragmentPatch1.FlowLabel == null) {
      return;
    }

    var val = SluiceFragmentPatch1.ThreadSafeWaterMap.WaterFlowDirection(
        ____sluice.GetComponentFast<BlockObject>().Coordinates);
    var currentStrength = Mathf.Max(Mathf.Abs(val.x), Mathf.Abs(val.y));
    SluiceFragmentPatch1.FlowLabel.text =
        ____loc.T(WaterCurrentLocKey, CommonFormats.FormatSmallValue(currentStrength));
  }
}

}
