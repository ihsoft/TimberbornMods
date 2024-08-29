// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower.UI {

/// <summary>Adds smart mechanical building status to the stock consumer building UI fragment.</summary>
[HarmonyPatch]
static class ConsumerFragmentServicePatch {
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.MechanicalSystemUI.ConsumerFragmentService:Update");
  }

  static void Postfix(MechanicalNode mechanicalNode, Label ____label, ILoc ____loc) {
    if (____label.style.display == DisplayStyle.None) {
      return;
    }
    var text = StateTextFormatter.FormatBuildingText(mechanicalNode, ____loc);
    if (text != null) {
      ____label.text += "\n" + text;
    }
  }
}

}
