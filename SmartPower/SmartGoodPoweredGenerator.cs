// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.MechanicalSystem;
using Timberborn.PowerGenerating;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

/// <summary>Smart version of the stock power generator.</summary>
/// <remarks>
/// It will check the actual demand and supply and only stop/start as many generators as needed to satisfy the demand.
/// The checking algorithm doesn't take into account the power in the batteries.
/// </remarks>
public sealed class SmartGoodPoweredGenerator : GoodPoweredGenerator {
  const float ChargeThreshold = 0.9f;
  GoodConsumingBuilding _goodConsumingBuilding;
  MechanicalNode _mechanicalNode;
  int _maxPower;
  int _skipTicks;

  #region TickableComponent implementation
  /// <inheritdoc/>
  public override void Tick() {
    if (_mechanicalNode.Graph != null) {
      UpdateGoodConsumption();
    }
  }
  #endregion

  #region Implementation
  new void Awake() {
    base.Awake();
    _goodConsumingBuilding = GetComponentFast<GoodConsumingBuilding>();
    _mechanicalNode = GetComponentFast<MechanicalNode>();
    _maxPower = GetComponentFast<MechanicalNodeSpecification>().PowerOutput;
    enabled = true;
  }

  void UpdateGoodConsumption() {
    if (_skipTicks > 0) {
      --_skipTicks;
      return;
    }
    var currentPower = _mechanicalNode.Graph.CurrentPower;
    var demand = currentPower.PowerDemand;
    var supply = currentPower.PowerSupply;
    var hasUnchargedBatteries = HasUnchargedBatteries();
    if (_goodConsumingBuilding.ConsumptionPaused) {
      if (demand <= supply && !hasUnchargedBatteries) {
        return;
      }
      HostedDebugLog.Fine(this, "Start good consumption: demand={0}, supply={1}", demand, supply);
      _goodConsumingBuilding.ResumeConsumption();
      if (_goodConsumingBuilding.HoursUntilNoSupply > 0) {
        _mechanicalNode.Active = true;
        _mechanicalNode.UpdateOutput(1.0f); // Be optimistic, let it update in the next tick.
        _skipTicks = 1;
      }
    } else {
      if (demand > supply - _maxPower || hasUnchargedBatteries) {
        return;
      }
      HostedDebugLog.Fine(this, "Stop good consumption: demand={0}, supply={1}", demand, supply);
      _goodConsumingBuilding.PauseConsumption();
      _mechanicalNode.UpdateOutput(0);  // The graph will be updated on the next tick.
      _skipTicks = 1;
    }

    bool HasUnchargedBatteries() {
      return _mechanicalNode.Graph.BatteryControllers
          .Any(ctrl => ctrl.Operational && ctrl.NormalizedCharge < ChargeThreshold);
    }
  }
  #endregion
}

}
