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

namespace IgorZ.SmartPower.UI;

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

  static void Postfix(bool __runOriginal, MechanicalNode mechanicalNode, Label ____label, ILoc ____loc) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
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