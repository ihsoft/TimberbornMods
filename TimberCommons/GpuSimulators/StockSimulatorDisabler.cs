// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.SimulationSystem;
using Timberborn.SoilContaminationSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.WaterSystem;

// ReSharper disable UnusedMember.Local
namespace IgorZ.TimberCommons.GpuSimulators {

// [HarmonyPatch(
//     typeof(SimulationController),
//     nameof(SimulationController.ParallelTick))]
// sealed class SimulationControllerPatch {
//   static bool Prefix() {
//     return !GpuSimulatorsController.Self.SimulatorEnabled;
//   }
// }

[HarmonyPatch(typeof(SoilMoistureSimulator), nameof(SoilMoistureSimulator.TickSimulation))]
sealed class SoilMoistureSimulatorPatch {
  static bool Prefix() {
    return !GpuSimulatorsController.Self.SimulatorEnabled;
  }
}

[HarmonyPatch(typeof(SoilContaminationSimulator), nameof(SoilContaminationSimulator.TickSimulation))]
sealed class SoilContaminationSimulatorPatch {
  static bool Prefix() {
    return !GpuSimulatorsController.Self.SimulatorEnabled;
  }
}

[HarmonyPatch(typeof(WaterSimulator), nameof(WaterSimulator.TickSimulation))]
sealed class WaterSimulatorPatch {
  static bool Prefix() {
    return !GpuSimulatorsController.Self.SimulatorEnabled;
  }
}

}
