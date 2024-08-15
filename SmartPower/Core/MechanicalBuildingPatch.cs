// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.MechanicalSystem;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

[HarmonyPatch(typeof(MechanicalBuilding), nameof(MechanicalBuilding.UpdateNodeCharacteristics))]
sealed class MechanicalBuildingPatch {
  // ReSharper disable once UnusedMember.Local
  static bool Prefix(BaseComponent __instance, MechanicalNode ____mechanicalNode) {
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

}
