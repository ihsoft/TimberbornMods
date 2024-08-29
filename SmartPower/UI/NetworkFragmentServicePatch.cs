// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower.UI {

/// <summary>Add battery status information to the stock mechanical node UI fragment.</summary>
[HarmonyPatch]
static class NetworkFragmentServicePatch {
  const float UpdateThreshold = 0.2f;
  static float _lastUpdate;
  static string _lastState = "";
  static MechanicalNode _lastMechanicalNode;

  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.MechanicalSystemUI.NetworkFragmentService:Update");
  }

  static void Postfix(MechanicalNode mechanicalNode, Label ____label, ILoc ____loc) {
    if (____label.style.display == DisplayStyle.None) {
      return;
    }
    if (_lastUpdate + UpdateThreshold < Time.unscaledTime || _lastMechanicalNode != mechanicalNode) {
      _lastUpdate = Time.unscaledTime;
      _lastMechanicalNode = mechanicalNode;
      _lastState = StateTextFormatter.FormatBatteryText(mechanicalNode, ____loc);
    }
    if (_lastState != null) {
      ____label.text += "\n" + _lastState;
    }
  }
}

}
