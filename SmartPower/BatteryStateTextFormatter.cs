// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using UnityEngine.UIElements;

namespace SmartPower {

/// <summary>Provides status strings for the battery state.</summary>
public static class BatteryStateTextFormatter {
  static readonly string ArrowUp = TextColors.ColorizeText("<GreenHighlight>🡅</GreenHighlight>");
  static readonly string ArrowDown = TextColors.ColorizeText("<RedHighlight>🡇</RedHighlight>");

  const string PowerSymbolLocKey = "Mechanical.PowerSymbol";
  const string PowerCapacitySymbolLocKey = "Mechanical.PowerCapacitySymbol";
  const string HourShortLocKey = "Time.HourShort";
  const string BatteryInfoLocKey = "IgorZ.SmartPower.BatteryInfo";
  const string BatteryLifeLocKey = "IgorZ.SmartPower.BatteryLife";

  /// <summary>Makes a formatted string that describes the current state of the batteries in the graph.</summary>
  /// <returns>Empty string if there are no batteries in the graph.</returns>
  public static string FormatBatteryText(MechanicalNode mechanicalNode, ILoc loc) {
    if (mechanicalNode.Graph.BatteryControllers.IsEmpty()) {
      return "";
    }
    var currentPower = mechanicalNode.Graph.CurrentPower;
    string flowStr;
    var lifetimeStr = "";
    if (currentPower.BatteryPower > 0) {
      flowStr = $"{ArrowDown}{currentPower.BatteryPower:0} {loc.T(PowerSymbolLocKey)}";
      var lifetime = currentPower.BatteryCharge / currentPower.BatteryPower;
      lifetimeStr = "\n" + loc.T(BatteryLifeLocKey, $"{lifetime:0.0}{loc.T(HourShortLocKey)}");
    } else {
      var flow = currentPower.PowerSupply - currentPower.PowerDemand;
      flowStr = flow <= 0 ? $"0 {loc.T(PowerSymbolLocKey)}" : $"{ArrowUp}{flow} {loc.T(PowerSymbolLocKey)}";
    }
    var capacityStr = $"{currentPower.BatteryCharge:0} {loc.T(PowerCapacitySymbolLocKey)}";
    var batteryInfo = loc.T(BatteryInfoLocKey, capacityStr, flowStr);
    return $"{batteryInfo}{lifetimeStr}";
  }
}

}
