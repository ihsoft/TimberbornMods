// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;

namespace IgorZ.SmartPower.UI;

/// <summary>Provides formatting methods for the various states.</summary>
public static class StateTextFormatter {
  const string PowerSymbolLocKey = "Mechanical.PowerSymbol";
  const string PowerCapacitySymbolLocKey = "Mechanical.PowerCapacitySymbol";
  const string HourShortLocKey = "Time.HourShort";

  const string BatteryCapacityLocKey = "IgorZ.SmartPower.BatteryCapacity";
  const string BatteryCharging = "IgorZ.SmartPower.BatteryCharging";
  const string BatteryDischarging = "IgorZ.SmartPower.BatteryDischarging";
  const string BatteryNotUsedLocKey = "IgorZ.SmartPower.BatteryNotUsed";
  const string BatteryDepletedLocKey = "IgorZ.SmartPower.BatteryDepleted";

  /// <summary>Makes a formatted string that describes the current state of the batteries in the graph.</summary>
  /// <returns><c>null</c> if there are no batteries in the graph.</returns>
  public static string FormatBatteryText(MechanicalNode mechanicalNode, ILoc loc) {
    if (mechanicalNode.Graph.BatteryControllers.IsEmpty()) {
      return null;
    }
    var batteryTotalCapacity = mechanicalNode.Graph.BatteryControllers
        .Where(x => x.Operational)
        .Select(x => x.Capacity)
        .Sum();
    if (batteryTotalCapacity == 0) {
      return null;
    }

    var currentPower = mechanicalNode.Graph.CurrentPower;
    var totalChargeStr = $"{currentPower.BatteryCharge:0} {loc.T(PowerCapacitySymbolLocKey)}";
    var batteryCapacityStr = loc.T(BatteryCapacityLocKey, batteryTotalCapacity, totalChargeStr);
    var batteryPowerNeed = currentPower.PowerDemand - currentPower.PowerSupply;

    // The network is consuming battery power.
    if (batteryPowerNeed > 0 && currentPower.BatteryCharge > float.Epsilon) {
      var timeLeft = currentPower.BatteryCharge / batteryPowerNeed;
      var flowStr = loc.T(
          BatteryDischarging,
          $"{batteryPowerNeed} {loc.T(PowerSymbolLocKey)}",
          $"{timeLeft:0.0}{loc.T(HourShortLocKey)}");
      return $"{batteryCapacityStr}\n{flowStr}";
    }

    // Some discharged batteries being charged using the excess power.
    if (batteryPowerNeed < 0 && currentPower.BatteryCharge < batteryTotalCapacity) {
      var timeLeft = (batteryTotalCapacity - currentPower.BatteryCharge) / -batteryPowerNeed;
      var flowStr = loc.T(
          BatteryCharging,
          $"{-batteryPowerNeed} {loc.T(PowerSymbolLocKey)}",
          $"{timeLeft:0.0}{loc.T(HourShortLocKey)}");
      return $"{batteryCapacityStr}\n{flowStr}";
    }

    // Batteries depleted.
    if (batteryPowerNeed > 0 && currentPower.BatteryCharge < float.Epsilon) {
      return $"{batteryCapacityStr}\n{loc.T(BatteryDepletedLocKey)}";
    }

    // Idle battery state.
    return $"{batteryCapacityStr}\n{loc.T(BatteryNotUsedLocKey)}";
  }
}
