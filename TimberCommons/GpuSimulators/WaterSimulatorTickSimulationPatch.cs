// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.WaterSystem;

namespace IgorZ.TimberCommons.GpuSimulators {

/// <summary>Intercepts stock game simulation thread to run a custom simulator.</summary>
[HarmonyPatch(typeof(WaterSimulator), nameof(WaterSimulator.TickSimulation))]
public class WaterSimulatorTickSimulationPatch {
  // ReSharper disable once UnusedMember.Local
  static bool Prefix() {
    return !GpuSimulatorsController.Self.WaterSimulatorEnabled;
  }
}

}
