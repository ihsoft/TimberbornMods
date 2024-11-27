// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.SmartPower.PowerConsumers;
using IgorZ.SmartPower.PowerGenerators;
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

  const string NoWorkersLocKey = "IgorZ.SmartPower.MechanicalBuilding.NoWorkersStatus";
  const string NoFuelLocKey = "IgorZ.SmartPower.MechanicalBuilding.NoFuelStatus";
  const string NoInputModeLocKey = "IgorZ.SmartPower.MechanicalBuilding.NoInputStatus";
  const string BlockedOutputLocKey = "IgorZ.SmartPower.MechanicalBuilding.BlockedOutputStatus";
  const string NotEnoughPowerLocKey = "IgorZ.SmartPower.PowerInputLimiter.NotEnoughPowerStatus";
  const string LowBatteriesChargeLocKey = "IgorZ.SmartPower.PowerInputLimiter.LowBatteriesChargeStatus";
  const string MinutesTillResumeLocKey = "IgorZ.SmartPower.Common.MinutesTillResume";
  const string MinutesTillSuspendLocKey = "IgorZ.SmartPower.Common.MinutesTillSuspend";

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

  /// <summary>Makes a formatted string that describes the power saving mode reason.</summary>
  /// <returns><c>null</c> if the building is not in power-saving mode or if the building is not compatible.</returns>
  public static string FormatConsumerBuildingText(MechanicalNode mechanicalNode, ILoc loc) {
    if (!mechanicalNode.IsConsumer) {
      return null;
    }
    var lines = new List<string>();

    var smartManufactory = mechanicalNode.GetComponentFast<SmartManufactory>();
    if (smartManufactory && smartManufactory.StandbyMode) {
      if (smartManufactory.NoFuel) {
        lines.Add(loc.T(NoFuelLocKey));
      } else if (smartManufactory.MissingIngredients) {
        lines.Add(loc.T(NoInputModeLocKey));
      } else if (smartManufactory.BlockedOutput) {
        lines.Add(loc.T(BlockedOutputLocKey));
      } else if (smartManufactory.AllWorkersOut) {
        lines.Add(loc.T(NoWorkersLocKey));
      }
    }

    return lines.Count > 0 ? string.Join("\n", lines) : null;
  }
}
