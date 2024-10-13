// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using Timberborn.BaseComponentSystem;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.SmartPower.PowerGenerators;

[HarmonyPatch]
static class GoodPoweredGeneratorPatch {
  static MethodBase TargetMethod() {
    return AccessTools.Method("Timberborn.PowerGenerating.GoodPoweredGenerator:Tick");
  }

  static bool Prefix(bool __runOriginal, BaseComponent __instance) {
    if (!__runOriginal) {
      return false;  // The other patches must follow the same style to properly support the skip logic!
    }
    var balancer = __instance.GetComponentFast<PowerOutputBalancer>();
    return !balancer.Automate;
  }
}
