﻿// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using IgorZ.TimberCommons.GpuSimulators;
using Timberborn.SoilMoistureSystem;

namespace IgorZ.TimberCommons.MultiThreadSimulators {

/// <summary>Intercepts stock game simulation thread to run a custom simulator.</summary>
[HarmonyPatch(typeof(SoilMoistureSimulator), nameof(SoilMoistureSimulator.TickSimulation))]
sealed class ParallelSoilMoistureSimulatorPatch {
  static ParallelSoilMoistureSimulator _patchedSimulator;
  internal static bool UsePatchedSimulator;

  internal static void Initialize() {
    _patchedSimulator = null;
  }

  // ReSharper disable once UnusedMember.Local
  // ReSharper disable once InconsistentNaming
  static bool Prefix(SoilMoistureSimulator __instance) {
    if (!UsePatchedSimulator || GpuSimulatorsController.Self.MoistureSimulatorEnabled) {
      return true;
    }
    _patchedSimulator ??= new ParallelSoilMoistureSimulator(__instance);
    _patchedSimulator.TickSimulation();
    return false;
  }
}

}