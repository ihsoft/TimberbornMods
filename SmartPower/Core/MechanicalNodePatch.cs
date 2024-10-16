﻿// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using Timberborn.MechanicalSystem;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.SmartPower.Core;

/// <summary>Detects when the mechanical node graph is changed and notifies the smart power service about it.</summary>
[HarmonyPatch]
static class MechanicalNodePatch {
  static MethodBase TargetMethod() {
    return AccessTools.PropertySetter("Timberborn.MechanicalSystem.MechanicalNode:Graph");
  }

  static void Postfix(MechanicalNode __instance) {
    SmartPowerService.OnMechanicalNodeGraphChanged(__instance);
  }
}
