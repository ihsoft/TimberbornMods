// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using Timberborn.BaseComponentSystem;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.SmartPower.Core {

[HarmonyPatch]
static class WalkerPoweredGeneratorPatch {
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.PowerGenerating.WalkerPoweredGenerator:Tick");
  }

  static void Postfix(BaseComponent __instance) {
    var autoPauseGenerator = __instance.GetComponentFast<AutoPausePowerGenerator>();
    if (autoPauseGenerator) {
      autoPauseGenerator.Decide();
    }
  }
}

}
