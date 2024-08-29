// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.SmartPower.Core {

/// <summary>
/// A component that tracks the network demand and supply provides a decision on whether to start or stop making power.
/// </summary>
/// <remarks>
/// This component doesn't affect anything by itself. It provides the <see cref="GetDecision"/> method tells what's the
/// current suggested decision. If the caller accepts the decision, it should act on it and call
/// <see cref="AcceptDecision"/> to indicate that the decision was accepted. 
/// </remarks>
/// <seealso cref="AutoPausePowerGenerator"/>
public sealed class PowerOutputBalancer : BaseComponent, IPersistentEntity {
  const float MaxBatteryChargeRatio = 0.9f;
  const float MinBatteryChargeRatio = 0.65f;

  #region IPersistentEntity implemenatation

  static readonly ComponentKey AutomationBehaviorKey = new(typeof(SmartGoodPoweredGenerator).FullName);
  static readonly PropertyKey<bool> AutomateKey = new(nameof(Automate));
  static readonly PropertyKey<float> ChargeBatteriesThresholdKey = new(nameof(ChargeBatteriesThreshold));
  static readonly PropertyKey<float> DischargeBatteriesThresholdKey = new(nameof(DischargeBatteriesThreshold));

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var saver = entitySaver.GetComponent(AutomationBehaviorKey);
    saver.Set(AutomateKey, Automate);
    saver.Set(ChargeBatteriesThresholdKey, ChargeBatteriesThreshold);
    saver.Set(DischargeBatteriesThresholdKey, DischargeBatteriesThreshold);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.HasComponent(AutomationBehaviorKey)) {
      return;
    }
    var state = entityLoader.GetComponent(AutomationBehaviorKey);
    Automate = state.GetValueOrNullable(AutomateKey) ?? false;
    ChargeBatteriesThreshold = state.GetValueOrNullable(ChargeBatteriesThresholdKey) ?? MaxBatteryChargeRatio;
    DischargeBatteriesThreshold = state.GetValueOrNullable(DischargeBatteriesThresholdKey) ?? MinBatteryChargeRatio;
  }

  #endregion

  #region API

  /// <summary>Indicates if the power deman/supply should be running even if the building is paused.</summary>
  /// <remarks>Enable it if the power saving logic should be applied to the paused buildings.</remarks>
  [SerializeField]
  [Tooltip("Enable it if the power saving logic should be applied to the paused buildings.")]
  public bool runWhenPaused;

  /// <summary>Delay in ticks to attempt the next power distribution check.</summary>
  [SerializeField]
  [Tooltip("Delay in ticks to attempt the next power distribution check.")]
  public int waitTicks = 1;

  /// <summary>Tells the generator should automatically paused/unpaused based on the power demand.</summary>
  public bool Automate { get; set; }

  /// <summary>The maximum level to which this generator should charge the batteries.</summary>
  public float ChargeBatteriesThreshold { get; set; } = MaxBatteryChargeRatio;

  /// <summary>The minimum level to let the batteries to discharge to.</summary>
  public float DischargeBatteriesThreshold { get; set; } = MinBatteryChargeRatio;

  /// <summary>Returns regulators in the same network.</summary>
  public IEnumerable<PowerOutputBalancer> AllBalancers => _mechanicalNode.Graph.Nodes
      .Select(x => x.GetComponentFast<PowerOutputBalancer>())
      .Where(x => x != null);

  /// <summary>Indicates what action should be taken by the generator.</summary>
  public enum Decision {
    /// <summary>No action should be taken.</summary>
    None,
    /// <summary>Start making power.</summary>
    StartMakingPower,
    /// <summary>Stop making power.</summary>
    StopMakingPower,
  }

  /// <summary>Tells the balancer that the decision was accepted and the generator is acting on it.</summary>
  public void AcceptDecision(Decision newDecision) {
    if (_activeDecision != newDecision) {
      HostedDebugLog.Fine(this, "Accepted new decision: {0} => {1}", _activeDecision, newDecision);
      _activeDecision = newDecision;
    }
  }

  /// <summary>
  /// Determines what action should be taken by the generator based on the current power demand/supply.
  /// </summary>
  public Decision GetDecision() {
    if (!Automate || !runWhenPaused && _pausable && _pausable.Paused || _mechanicalNode.Graph == null) {
      _lastDecision = Decision.None;
      _skipTicks = 0;
      return Decision.None;
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

    var newDecision = Decision.None;
    var needPower = demand - supply;
    if (!hasBatteries && needPower > 0 || hasBatteries && batteriesChargeRatio <= DischargeBatteriesThreshold) {
      newDecision = Decision.StartMakingPower;
    }
    //FIXME: get from IGenerator?
    var excessPower = supply - _maxPower;
    if (!hasBatteries && excessPower >= demand 
        || hasBatteries && batteriesChargeRatio >= ChargeBatteriesThreshold) {
      newDecision = Decision.StopMakingPower;
    }
    if (newDecision != _lastDecision) {
      _lastDecision = newDecision;
      _skipTicks = waitTicks;
      return _activeDecision;
    }
    // Don't immediately accept the decision to avoid flickering.
    if (_skipTicks > 0) {
      _skipTicks--;
      return _activeDecision;
    }
    return newDecision;
  }

  #endregion

  #region Implementation

  MechanicalNode _mechanicalNode;
  PausableBuilding _pausable;
  int _skipTicks;
  int _maxPower;

  Decision _lastDecision = Decision.None;
  Decision _activeDecision = Decision.None;

  void Awake() {
    _mechanicalNode = GetComponentFast<MechanicalNode>();
    _maxPower = GetComponentFast<MechanicalNodeSpecification>().PowerOutput;
    _pausable = GetComponentFast<PausableBuilding>();
  }

  #endregion
}

}
