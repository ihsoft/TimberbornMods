// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberCommons.Common;
using Timberborn.CoreUI;
using Timberborn.Workshops;
using Timberborn.WorkshopsUI;
using UnityEngine.UIElements;
using ProgressBar = Timberborn.CoreUI.ProgressBar;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.IrrigationSystemUI {

/// <summary>Handles "supply left" element in the manufactory UI fragment.</summary>
/// <seealso cref="ManufactoryInventoryFragmentInitializeFragmentPatch"/>
[HarmonyPatch(typeof(ManufactoryInventoryFragment), nameof(ManufactoryInventoryFragment.UpdateFragment))]
static class ManufactoryInventoryFragmentUpdateFragmentPatch {
  internal static ProgressBar HoursLeftBar;
  internal static Label HoursLeftLabel;

  // ReSharper disable once UnusedMember.Local
  static void Postfix(bool __runOriginal, Manufactory ____manufactory) {
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
