// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.SmartPower.PowerGenerators;

[HarmonyPatch]
static class GoodPoweredGeneratorPatch {
  static MethodBase TargetMethod() {
    return AccessTools.Method("Timberborn.PowerGenerating.GoodPoweredGenerator:Tick");
  }

  static bool Prefix() {
    // All power saving logic is run by the SmartPowerService.
    return false;
  }
}
