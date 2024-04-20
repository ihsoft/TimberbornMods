// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.SoilContaminationSystem;

namespace IgorZ.TimberCommons.GpuSimulators {

/// <summary>Intercepts stock game simulation thread to run a custom simulator.</summary>
[HarmonyPatch(typeof(SoilContaminationSimulator), nameof(SoilContaminationSimulator.TickSimulation))]
public class SoilContaminationSimulatorTickSimulationPatch {
  // ReSharper disable once UnusedMember.Local
  static bool Prefix() {
    return !GpuSoilContaminationSimulator.Self.IsEnabled;
  }
}

}
