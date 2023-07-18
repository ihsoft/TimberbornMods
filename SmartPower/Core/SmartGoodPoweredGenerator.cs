// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.PowerGenerating;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

/// <summary>Smart version of the stock power generator.</summary>
/// <remarks>
/// It will check the actual demand and supply and only stop/start as many generators as needed to satisfy the demand.
/// The checking algorithm doesn't take into account the power in the batteries.
/// </remarks>
public sealed class SmartGoodPoweredGenerator : GoodPoweredGenerator, IPersistentEntity {
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

  #region IPersistentEntity implemenatation
  static readonly ComponentKey AutomationBehaviorKey = new(typeof(SmartGoodPoweredGenerator).FullName);
  static readonly PropertyKey<bool> NeverShutdownKey = new(nameof(NeverShutdown));
  static readonly PropertyKey<float> ChargeBatteriesThresholdKey = new(nameof(ChargeBatteriesThreshold));

  public void Save(IEntitySaver entitySaver) {
    var saver = entitySaver.GetComponent(AutomationBehaviorKey);
    saver.Set(NeverShutdownKey, NeverShutdown);
    saver.Set(ChargeBatteriesThresholdKey, ChargeBatteriesThreshold);
  }

  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.HasComponent(AutomationBehaviorKey)) {
      return;
    }
    var state = entityLoader.GetComponent(AutomationBehaviorKey);
    NeverShutdown = state.GetValueOrNullable(NeverShutdownKey) ?? false;
    ChargeBatteriesThreshold = state.GetValueOrNullable(ChargeBatteriesThresholdKey) ?? 0.9f;
  }
  #endregion

  #region API
  /// <summary>Tells the smart logic to never shutdown this generator.</summary>
  public bool NeverShutdown { get; set; }

  /// <summary>Sets the maximum level to which this generator should charge teh batteries.</summary>
  public float ChargeBatteriesThreshold { get; set; }
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
    var batteryCapacity = 0;
    var batteryCharge = 0.0f;
    foreach (var batteryCtrl in _mechanicalNode.Graph.BatteryControllers) {
      if (batteryCtrl.Operational) {
        batteryCapacity += batteryCtrl.Capacity;
        batteryCharge += batteryCtrl.Charge;
      }
    }
    var needChargedBatteries =
        ChargeBatteriesThreshold > 0 && batteryCharge < batteryCapacity * ChargeBatteriesThreshold;
    if (_goodConsumingBuilding.ConsumptionPaused) {
      if (demand <= supply && !needChargedBatteries && !NeverShutdown) {
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
      if (demand > supply - _maxPower || needChargedBatteries || NeverShutdown) {
        return;
      }
      HostedDebugLog.Fine(this, "Stop good consumption: demand={0}, supply={1}", demand, supply);
      _goodConsumingBuilding.PauseConsumption();
      _mechanicalNode.UpdateOutput(0);  // The graph will be updated on the next tick.
      _skipTicks = 1;
    }
  }
  #endregion
}

}
