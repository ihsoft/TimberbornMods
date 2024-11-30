// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.SmartPower.Core;
using IgorZ.SmartPower.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using UnityEngine.UIElements;

namespace IgorZ.SmartPower.NetworkUI;

sealed class MechanicalNodeFragment : IEntityPanelFragment {

  const string PowerSymbolLocKey = "Mechanical.PowerSymbol";
  const string PowerCapacitySymbolLocKey = "Mechanical.PowerCapacitySymbol";
  const string HourShortLocKey = "Time.HourShort";

  const string BatteryCapacityLocKey = "IgorZ.SmartPower.BatteryCapacity";
  const string BatteryCharging = "IgorZ.SmartPower.BatteryCharging";
  const string BatteryDischarging = "IgorZ.SmartPower.BatteryDischarging";
  const string BatteryNotUsedLocKey = "IgorZ.SmartPower.BatteryNotUsed";
  const string BatteryDepletedLocKey = "IgorZ.SmartPower.BatteryDepleted";

  readonly UiFactory _uiFactory;
  readonly ILoc _loc;
  readonly SmartPowerService _smartPowerService;

  Label _batteryTextLabel;  // It is patched in the stock UI.
  PanelFragmentPatcher _batteryTextPatcher;

  MechanicalNode _mechanicalNode;

  MechanicalNodeFragment(UiFactory uiFactory, ILoc loc, SmartPowerService smartPowerService) {
    _uiFactory = uiFactory;
    _loc = loc;
    _smartPowerService = smartPowerService;
  }

  public VisualElement InitializeFragment() {
    var root = new VisualElement();
    root.ToggleDisplayStyle(visible: false);

    _batteryTextLabel = _uiFactory.CreateLabel();
    _batteryTextLabel.ToggleDisplayStyle(visible: false);
    _batteryTextPatcher = new PanelFragmentPatcher(
        _batteryTextLabel, root, PanelFragmentPatcher.MechanicalNodeFragmentName, "Network");

    return root;
  }

  public void ShowFragment(BaseComponent entity) {
    if (!BatteriesSettings.ShowBatteryVitals) {
      return;
    }
    _mechanicalNode = entity.GetComponentFast<MechanicalNode>();
    if (_mechanicalNode) {
      _batteryTextPatcher.Patch();
      _batteryTextLabel.ToggleDisplayStyle(visible: true);
    }
  }

  public void ClearFragment() {
    _batteryTextLabel.ToggleDisplayStyle(visible: false);
    _mechanicalNode = null;
  }

  public void UpdateFragment() {
    if (_mechanicalNode) {
      UpdateBatteryText();
    }
  }

  void UpdateBatteryText() {
    // var batteryTotalCapacity = _mechanicalNode.Graph.BatteryControllers
    //     .Where(x => x.Operational)
    //     .Select(x => x.Capacity)
    //     .Sum();
    _smartPowerService.GetBatteriesStat(_mechanicalNode.Graph, out var batteryTotalCapacity, out _);
    if (batteryTotalCapacity == 0) {
      _batteryTextLabel.ToggleDisplayStyle(visible: false);
      return;
    }

    var currentPower = _mechanicalNode.Graph.CurrentPower;
    var totalChargeStr = $"{currentPower.BatteryCharge:0} {_loc.T(PowerCapacitySymbolLocKey)}";
    var batteryCapacityStr = _loc.T(BatteryCapacityLocKey, batteryTotalCapacity, totalChargeStr);
    var batteryPowerNeed = currentPower.PowerDemand - currentPower.PowerSupply;
    string batteryStatus = null;

    if (batteryPowerNeed > 0 && currentPower.BatteryCharge > float.Epsilon) {
      // The network is consuming battery power.
      var timeLeft = currentPower.BatteryCharge / batteryPowerNeed;
      var flowStr = _loc.T(
          BatteryDischarging,
          $"{batteryPowerNeed} {_loc.T(PowerSymbolLocKey)}",
          $"{timeLeft:0.0}{_loc.T(HourShortLocKey)}");
      batteryStatus = $"{batteryCapacityStr}\n{flowStr}";
    } else if (batteryPowerNeed < 0 && currentPower.BatteryCharge < batteryTotalCapacity) {
      // Some discharged batteries being charged using the excess power.
      var timeLeft = (batteryTotalCapacity - currentPower.BatteryCharge) / -batteryPowerNeed;
      var flowStr = _loc.T(
          BatteryCharging,
          $"{-batteryPowerNeed} {_loc.T(PowerSymbolLocKey)}",
          $"{timeLeft:0.0}{_loc.T(HourShortLocKey)}");
      batteryStatus = $"{batteryCapacityStr}\n{flowStr}";
    } else if (batteryPowerNeed > 0 && currentPower.BatteryCharge < float.Epsilon) {
      // Batteries depleted.
      batteryStatus = $"{batteryCapacityStr}\n{_loc.T(BatteryDepletedLocKey)}";
    } else {
      // Idle battery state.
      batteryStatus = $"{batteryCapacityStr}\n{_loc.T(BatteryNotUsedLocKey)}";
    }
    
    _batteryTextLabel.text = batteryStatus;
    _batteryTextLabel.ToggleDisplayStyle(visible: true);
  }
}
