// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.SmartPower.Settings;
using IgorZ.TimberDev.Utils;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.MechanicalSystemUI;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.SmartPower.UI;

/// <summary>Add battery status information to the stock mechanical node UI fragment.</summary>
[HarmonyPatch(typeof(NetworkFragmentService), nameof(NetworkFragmentService.Update))]
static class NetworkFragmentServicePatch {
  static string _lastState = "";
  static MechanicalNode _lastMechanicalNode;
  static readonly TimedUpdater _updater = new(0.2f);

  static void Postfix(bool __runOriginal, MechanicalNode mechanicalNode, Label ____label, ILoc ____loc) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (!NetworkUISettings.ShowBatteryVitals || ____label.style.display == DisplayStyle.None) {
      return;
    }
    _updater.Update(
        () => {
          _lastState = StateTextFormatter.FormatBatteryText(mechanicalNode, ____loc);
        }, force: _lastMechanicalNode != mechanicalNode);
    _lastMechanicalNode = mechanicalNode;
    if (_lastState != null) {
      ____label.text += "\n" + _lastState;
    }
  }
}
