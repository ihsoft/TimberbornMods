// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.SoilMoistureSystem;

namespace IgorZ.TimberCommons.GpuSimulators {

/// <summary>Intercepts stock game simulation thread to run a custom simulator.</summary>
[HarmonyPatch(typeof(SoilMoistureSimulator), nameof(SoilMoistureSimulator.TickSimulation))]
public class SoilMoistureSimulatorTickSimulationPatch {
  // ReSharper disable once UnusedMember.Local
  static bool Prefix() {
    return !GpuSimulatorsController.Self.MoistureSimulatorEnabled;
  }
}

}
