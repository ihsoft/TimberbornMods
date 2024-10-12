// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.SmartPower.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartPower.PowerGenerators;

abstract class PowerOutputBalancer : BaseComponent, IPersistentEntity, ISuspendableGenerator, IFinishedStateListener {
  const float MaxBatteryChargeRatio = 0.9f;
  const float MinBatteryChargeRatio = 0.65f;

  #region API

  /// <summary>Tells the generator should automatically pause/unpause based on the power demand.</summary>
  public bool Automate {
    get => _automate;
    set {
      _automate = value;
      UpdateRegistration();
    }
  }
  bool _automate;

  /// <inheritdoc/>
  public float ChargeBatteriesThreshold { get; set; } = MaxBatteryChargeRatio;

  /// <inheritdoc/>
  public float DischargeBatteriesThreshold { get; set; } = MinBatteryChargeRatio;

  /// <inheritdoc/>
  //public MechanicalNode MechanicalNode => _mechanicalNode;
  public MechanicalNode MechanicalNode { get; private set; }

  /// <inheritdoc/>
  public int NominalOutput { get; private set; }

  /// <inheritdoc/>
  public bool IsSuspended { get; private set; }

  /// <summary>Returns balancers in the same network.</summary>
  // FIXME: also check buildings name.
  public IEnumerable<PowerOutputBalancer> AllBalancers => MechanicalNode.Graph.Nodes
      .Select(x => x.GetComponentFast<PowerOutputBalancer>())
      .Where(x => x != null);

  /// <inheritdoc/>
  public abstract int Priority { get; }

  /// <inheritdoc/>
  public virtual void Suspend() {
    IsSuspended = true;
  }

  /// <inheritdoc/>
  public virtual void Resume() {
    IsSuspended = false;
  }

  #endregion

  #region Inheritables

  /// <summary>The service to register/unregister the generator in the system.</summary>
  protected SmartPowerService SmartPowerService { get; private set; }

  /// <summary>
  /// Called when generator state changes, and it may be needed to register or unregister it in the system.
  /// </summary>
  /// <seealso cref="Automate"/>
  /// <seealso cref="SmartPowerService"/>
  /// FIXME
  protected abstract void UpdateRegistration();

  #endregion
  
  #region IComparable implementation

  public int CompareTo(ISuspendableGenerator other) {
    var priorityCheck = Priority.CompareTo(other.Priority);
    if (priorityCheck != 0) {
      return priorityCheck;
    }
    var powerCheck = NominalOutput.CompareTo(other.NominalOutput);
    return powerCheck == 0 ? GetHashCode().CompareTo(other.GetHashCode()) : powerCheck;
  }

  #endregion

  #region IFinalizableStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    UpdateRegistration();
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    SmartPowerService.UnregisterGenerator(this);
  }

  #endregion

  #region IPersistentEntity implemenatation

  static readonly ComponentKey AutomationBehaviorKey = new(typeof(PowerOutputBalancer).FullName);
  static readonly PropertyKey<bool> AutomateKey = new("Automate");
  static readonly PropertyKey<float> ChargeBatteriesThresholdKey = new("ChargeBatteriesThreshold");
  static readonly PropertyKey<float> DischargeBatteriesThresholdKey = new("DischargeBatteriesThreshold");
  static readonly PropertyKey<bool> IsSuspendedKey = new("IsSuspended");

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var saver = entitySaver.GetComponent(AutomationBehaviorKey);
    saver.Set(AutomateKey, Automate);
    saver.Set(ChargeBatteriesThresholdKey, ChargeBatteriesThreshold);
    saver.Set(DischargeBatteriesThresholdKey, DischargeBatteriesThreshold);
    saver.Set(IsSuspendedKey, IsSuspended);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.HasComponent(AutomationBehaviorKey)) {
      return;
    }
    var state = entityLoader.GetComponent(AutomationBehaviorKey);
    _automate = state.GetValueOrNullable(AutomateKey) ?? false;
    ChargeBatteriesThreshold = state.GetValueOrNullable(ChargeBatteriesThresholdKey) ?? MaxBatteryChargeRatio;
    DischargeBatteriesThreshold = state.GetValueOrNullable(DischargeBatteriesThresholdKey) ?? MinBatteryChargeRatio;
    IsSuspended = state.GetValueOrNullable(IsSuspendedKey) ?? false;
  }

  #endregion

  #region Implementation

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(SmartPowerService smartPowerService) {
    SmartPowerService = smartPowerService;
  }

  protected virtual void Awake() {
    MechanicalNode = GetComponentFast<MechanicalNode>();
    NominalOutput = GetComponentFast<MechanicalNodeSpecification>().PowerOutput;
  }

  #endregion
}
