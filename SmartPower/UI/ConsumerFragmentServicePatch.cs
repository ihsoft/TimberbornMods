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

/// <summary>Adds smart mechanical building status to teh stock consumer building UI fragment.</summary>
[HarmonyPatch]
static class ConsumerFragmentServicePatch {
  const string NetworkFragmentServiceClassName = "Timberborn.MechanicalSystemUI.ConsumerFragmentService";
  const string MethodName = "Update";

  static MethodBase TargetMethod() {
    var type = AccessTools.TypeByName(NetworkFragmentServiceClassName);
    return AccessTools.FirstMethod(type, method => method.Name == MethodName);
  }

  static void Postfix(MechanicalNode mechanicalNode, Label ____label, ILoc ____loc) {
    var text = StateTextFormatter.FormatBuildingText(mechanicalNode, ____loc);
    if (text != null) {
      ____label.text += "\n" + text;
    }
  }
}

}
