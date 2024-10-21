// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.MechanicalSystem;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.SmartPower.Core;

[HarmonyPatch(typeof(MechanicalBuilding), nameof(MechanicalBuilding.UpdateNodeCharacteristics))]
static class MechanicalBuildingPatch {
  // This method needs publicized Timberborn.MechanicalSystem.
  static bool Prefix(bool __runOriginal, BaseComponent __instance, MechanicalNode ____mechanicalNode) {
    if (!__runOriginal) {
      return false;  // The other patches must follow the same style to properly support the skip logic!
    }
    var adjustablePowerInput = __instance.GetComponentFast<IAdjustablePowerInput>();
    if (adjustablePowerInput == null) {
      return true;
    }
    var newPowerInput = adjustablePowerInput.UpdateAndGetPowerInput(____mechanicalNode._nominalPowerInput);
    if (newPowerInput == ____mechanicalNode.PowerInput) {
      return false;
    }
    HostedDebugLog.Fine(
      __instance, "Adjusting power input from {0} to {1}", ____mechanicalNode.PowerInput, newPowerInput);
    var graph = ____mechanicalNode.Graph;
    graph?.DeactivateNode(____mechanicalNode);
    ____mechanicalNode.PowerInput = newPowerInput;
    graph?.ActivateNode(____mechanicalNode);
    return false;
  }
}