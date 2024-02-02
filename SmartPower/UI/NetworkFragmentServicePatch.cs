// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.MechanicalSystemUI;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower.UI {

/// <summary>Add battery status information to the stock mechanical node UI fragment.</summary>
[HarmonyPatch(typeof(NetworkFragmentService), nameof(NetworkFragmentService.Update))]
static class NetworkFragmentServicePatch {
  // ReSharper disable once UnusedMember.Local
  static void Postfix(ref bool __runOriginal, MechanicalNode mechanicalNode, Label ____label, ILoc ____loc) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (____label.style.display == DisplayStyle.None) {
      return;
    }
    var text = StateTextFormatter.FormatBatteryText(mechanicalNode, ____loc);
    if (text != null) {
      ____label.text += "\n" + text;
    }
  }
}

}
