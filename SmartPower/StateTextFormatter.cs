// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

/// <summary>Provides formatting methods for the various states.</summary>
public static class StateTextFormatter {
  const string PowerSymbolLocKey = "Mechanical.PowerSymbol";
  const string PowerCapacitySymbolLocKey = "Mechanical.PowerCapacitySymbol";
  const string HourShortLocKey = "Time.HourShort";
  const string BatteryCapacityLocKey = "IgorZ.SmartPower.BatteryCapacity";
  const string BatteryCharging = "IgorZ.SmartPower.BatteryCharging";
  const string BatteryDischarging = "IgorZ.SmartPower.BatteryDischarging";

  /// <summary>Makes a formatted string that describes the current state of the batteries in the graph.</summary>
  /// <returns><c>null</c> if there are no batteries in the graph.</returns>
  public static string FormatBatteryText(MechanicalNode mechanicalNode, ILoc loc) {
    if (mechanicalNode.Graph.BatteryControllers.IsEmpty()) {
      return null;
    }
    var currentPower = mechanicalNode.Graph.CurrentPower;
    var batteryTotalCapacity = mechanicalNode.Graph.BatteryControllers
        .Where(x => x.Operational)
        .Select(x => x.Capacity)
        .Sum();
    var totalChargeStr = $"{currentPower.BatteryCharge:0} {loc.T(PowerCapacitySymbolLocKey)}";
    var batteryCapacityStr = loc.T(BatteryCapacityLocKey, batteryTotalCapacity, totalChargeStr);

    // Battery power is being consumed by the network.
    if (currentPower.BatteryPower > 0) {
      var timeLeft = currentPower.BatteryCharge / currentPower.BatteryPower;
      var flowStr = loc.T(
          BatteryDischarging,
          $"{currentPower.BatteryPower} {loc.T(PowerSymbolLocKey)}",
          $"{timeLeft:0.0}{loc.T(HourShortLocKey)}");
      return $"{batteryCapacityStr}\n{flowStr}";
    }

    // Some discharged batteries being charged using the excess power.
    var dischargedCapacity = batteryTotalCapacity - currentPower.BatteryCharge;
    if (currentPower.PowerSupply > currentPower.PowerDemand && dischargedCapacity > float.Epsilon) {
      var flow = currentPower.PowerSupply - currentPower.PowerDemand;
      var timeLeft = dischargedCapacity / flow;
      var flowStr = loc.T(
          BatteryCharging,
          $"{flow} {loc.T(PowerSymbolLocKey)}",
          $"{timeLeft:0.0}{loc.T(HourShortLocKey)}");
      return $"{batteryCapacityStr}\n{flowStr}";
    }

    // Idle battery state.
    return $"{batteryCapacityStr}";
  }
}

}
