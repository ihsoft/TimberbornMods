// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.PowerGenerating;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.SmartPower.PowerGenerators;

[HarmonyPatch(typeof(GoodPoweredGenerator), nameof(GoodPoweredGenerator.UpdateGoodConsumption))]
static class GoodPoweredGeneratorPatch {
  static bool Prefix(bool __runOriginal, BaseComponent __instance) {
    if (!__runOriginal) {
      return false;  // The other patches must follow the same style to properly support the skip logic!
    }
    var balancer = __instance.GetComponentFast<PowerOutputBalancer>();
    return !balancer.Automate;
  }
}
