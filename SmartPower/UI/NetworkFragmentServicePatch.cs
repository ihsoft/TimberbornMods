// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower.UI {

/// <summary>Add battery status information to the stock mechanical node UI fragment.</summary>
[HarmonyPatch]
static class NetworkFragmentServicePatch {
  const string NetworkFragmentServiceClassName = "Timberborn.MechanicalSystemUI.NetworkFragmentService";
  const string MethodName = "Update";

  static MethodBase TargetMethod() {
    var type = AccessTools.TypeByName(NetworkFragmentServiceClassName);
    return AccessTools.FirstMethod(type, method => method.Name == MethodName);
  }

  static void Postfix(MechanicalNode mechanicalNode, Label ____label, ILoc ____loc) {
    var text = StateTextFormatter.FormatBatteryText(mechanicalNode, ____loc);
    if (text != null) {
      ____label.text += "\n" + text;
    }
  }
}

}
