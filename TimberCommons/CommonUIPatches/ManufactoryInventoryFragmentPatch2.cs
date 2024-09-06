// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using IgorZ.TimberCommons.Common;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches {

/// <summary>Harmony patch to display "supply left" element in the manufactory UI fragment.</summary>
/// <seealso cref="ManufactoryInventoryFragmentPatch1"/>
[HarmonyPatch]
static class ManufactoryInventoryFragmentPatch2 {
  internal static Timberborn.CoreUI.ProgressBar HoursLeftBar;
  internal static Label HoursLeftLabel;

  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.WorkshopsUI.ManufactoryInventoryFragment:UpdateFragment");
  }

  static void Postfix(bool __runOriginal, BaseComponent ____manufactory) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (____manufactory == null || HoursLeftBar == null || HoursLeftLabel == null) {
      return;
    }

    var supplyLeftProvider = ____manufactory.GetComponentFast<ISupplyLeftProvider>();
    var visible = supplyLeftProvider != null;
    HoursLeftBar.ToggleDisplayStyle(visible: visible);
    if (!visible) {
      return;
    }
    var (progress, supplyLeft) = supplyLeftProvider.GetStats();
    if (progress < 0f || supplyLeft == null) {
      HoursLeftBar.ToggleDisplayStyle(visible: false);
      return;
    }
    HoursLeftBar.SetProgress(progress);
    HoursLeftLabel.text = supplyLeft;
  }
}

}
