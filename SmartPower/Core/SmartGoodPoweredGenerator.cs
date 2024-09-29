// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BuildingsBlocking;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.PowerGenerating;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartPower.Core {

/// <summary>Smart version of the stock power generator.</summary>
/// <remarks>
/// It will check the actual demand and supply and only stop/start as many generators as needed to satisfy the demand.
/// The checking algorithm doesn't take into account the power in the batteries.
/// </remarks>
public sealed class SmartGoodPoweredGenerator : GoodPoweredGenerator, IPersistentEntity {
  const float MaxBatteryChargeRatio = 0.9f;
  const float MinBatteryChargeRatio = 0.65f;

  GoodConsumingBuilding _goodConsumingBuilding;
  GoodConsumingToggle _goodConsumingToggle;
  MechanicalNode _mechanicalNode;
  PausableBuilding _pausable;
  int _maxPower;
  int _skipTicks;

  #region TickableComponent implementation

  /// <inheritdoc/>
  public override void Tick() {
    if ((_pausable == null || !_pausable.Paused) && _mechanicalNode.Graph != null) {
      UpdateGoodConsumption();
    }
  }
  #endregion

  #region IPersistentEntity implemenatation
  static readonly ComponentKey AutomationBehaviorKey = new(typeof(SmartGoodPoweredGenerator).FullName);
  static readonly PropertyKey<bool> NeverShutdownKey = new(nameof(NeverShutdown));
  static readonly PropertyKey<float> ChargeBatteriesThresholdKey = new(nameof(ChargeBatteriesThreshold));
  static readonly PropertyKey<float> DischargeBatteriesThresholdKey = new(nameof(DischargeBatteriesThreshold));

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var saver = entitySaver.GetComponent(AutomationBehaviorKey);
    saver.Set(NeverShutdownKey, NeverShutdown);
    saver.Set(ChargeBatteriesThresholdKey, ChargeBatteriesThreshold);
    saver.Set(DischargeBatteriesThresholdKey, DischargeBatteriesThreshold);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.HasComponent(AutomationBehaviorKey)) {
      return;
    }
    var state = entityLoader.GetComponent(AutomationBehaviorKey);
    NeverShutdown = state.GetValueOrNullable(NeverShutdownKey) ?? false;
    ChargeBatteriesThreshold = state.GetValueOrNullable(ChargeBatteriesThresholdKey) ?? MaxBatteryChargeRatio;
    DischargeBatteriesThreshold = state.GetValueOrNullable(DischargeBatteriesThresholdKey) ?? MinBatteryChargeRatio;
  }

  #endregion

  #region API

  /// <summary>Tells the smart logic to never shutdown this generator.</summary>
  public bool NeverShutdown { get; set; }

  /// <summary>The maximum level to which this generator should charge the batteries.</summary>
  public float ChargeBatteriesThreshold { get; set; } = MaxBatteryChargeRatio;

  /// <summary>The minimum level to let the batteries discharge to.</summary>
  public float DischargeBatteriesThreshold { get; set; } = MinBatteryChargeRatio;

  /// <summary>Returns the mechanical graph this generator is connected to.</summary>
  public MechanicalGraph MechanicalGraph => _mechanicalNode.Graph;

  #endregion

  #region Implementation

  new void Awake() {
    base.Awake();
    _goodConsumingBuilding = GetComponentFast<GoodConsumingBuilding>();
    _goodConsumingToggle = _goodConsumingBuilding.GetGoodConsumingToggle();
    _mechanicalNode = GetComponentFast<MechanicalNode>();
    _maxPower = GetComponentFast<MechanicalNodeSpecification>().PowerOutput;
    _pausable = GetComponentFast<PausableBuilding>();
    if (_pausable != null) {
      _pausable.PausedChanged += (_, _) => _goodConsumingToggle.ResumeConsumption();
    }
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
    var hasBatteries = false;
    foreach (var batteryCtrl in _mechanicalNode.Graph.BatteryControllers) {
      if (batteryCtrl.Operational) {
        batteryCapacity += batteryCtrl.Capacity;
        batteryCharge += batteryCtrl.Charge;
        hasBatteries = true;
      }
    }
    var batteriesChargeRatio = hasBatteries ? batteryCharge / batteryCapacity : -1;
    if (_goodConsumingBuilding.ConsumptionPaused) {
      // Resume if need power to charge batteries or cover shortage.
      if (!hasBatteries && demand > supply
          || hasBatteries && batteriesChargeRatio <= DischargeBatteriesThreshold
          || NeverShutdown) {
        HostedDebugLog.Fine(
            this, "Start good consumption: demand={0}, supply={1}, batteries={2:0.00}, threshold={3}",
            demand, supply, batteriesChargeRatio, DischargeBatteriesThreshold);
        _goodConsumingToggle.ResumeConsumption();
        if (_goodConsumingBuilding.HoursUntilNoSupply > 0) {
          _mechanicalNode.Active = true;
          _mechanicalNode.UpdateOutput(1.0f); // Be optimistic, let it update in the next tick.
          _skipTicks = 1;
        }
      }
    } else if (!NeverShutdown) {
      // Pause if no ways to spend the excess power from the generator.
      if (!hasBatteries && supply - _maxPower >= demand 
          || hasBatteries && batteriesChargeRatio >= ChargeBatteriesThreshold) {
        HostedDebugLog.Fine(
            this, "Stop good consumption: demand={0}, supply={1}, batteries={2:0.00}, check={3}",
            demand, supply, batteriesChargeRatio, ChargeBatteriesThreshold);
        _goodConsumingToggle.PauseConsumption();
        _mechanicalNode.UpdateOutput(0);  // The graph will be updated on the next tick.
        _skipTicks = 1;
      }
    }
  }
  #endregion
}

}
