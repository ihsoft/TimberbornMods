// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.SmartPower.Core;
using IgorZ.SmartPower.Settings;
using IgorZ.SmartPower.Utils;
using Timberborn.BlockSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.StatusSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartPower.PowerGenerators;

abstract class PowerOutputBalancer
    : TickableComponent, IPersistentEntity, IFinishedStateListener, IPostInitializableLoadedEntity {

  const float MaxBatteryChargeRatio = 0.9f;
  const float MinBatteryChargeRatio = 0.65f;

  #region API

  /// <summary>Indicates if the generator is currently suspended.</summary>
  public bool IsSuspended { get; private set; }

  /// <summary>The minimum level to let the batteries discharge to.</summary>
  public float DischargeBatteriesThreshold { get; set; } = MinBatteryChargeRatio;

  /// <summary>The maximum level to which this generator should charge the batteries.</summary>
  public float ChargeBatteriesThreshold { get; set; } = MaxBatteryChargeRatio;

  /// <summary>Indicates whether the generator should automatically pause/unpause based on power demand.</summary>
  public bool Automate {
    get => _automate;
    set {
      _automate = value;
      UpdateStatus();
    }
  }
  bool _automate;

  /// <summary>Returns the same balancers in the network.</summary>
  public IEnumerable<PowerOutputBalancer> AllBalancers => MechanicalNode.Graph.Nodes
      .Select(x => x.GetComponentFast<PowerOutputBalancer>())
      .Where(x => x != null && x.name == name);

  #endregion

  #region TickableComponent overrides

  /// <inheritdoc/>
  public override void StartTickable() {
    SetupDelays();
    base.StartTickable();
  }

  /// <inheritdoc/>
  public override void Tick() {
    if (SmartPowerService.SmartLogicStarted) {
      UpdateStatus();
    }
  }

  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    enabled = true;
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    enabled = false;
  }

  #endregion

  #region IPostInitializableLoadedEntity implementation

  /// <inheritdoc/>
  public void PostInitializeLoadedEntity() {
    if (enabled && IsSuspended) {
      Suspend();
    }
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
    _automate = state.GetValueOrNullable(AutomateKey) ?? _automate;
    ChargeBatteriesThreshold = state.GetValueOrNullable(ChargeBatteriesThresholdKey) ?? MaxBatteryChargeRatio;
    DischargeBatteriesThreshold = state.GetValueOrNullable(DischargeBatteriesThresholdKey) ?? MinBatteryChargeRatio;
    IsSuspended = state.GetValueOrNullable(IsSuspendedKey) ?? false;
  }

  #endregion

  #region Inheritable contract

  /// <summary>The mechanical node of the building.</summary>
  /// <remarks>
  /// The descendants should try to proactively update its power output when suspending/resuming. If not done, then the
  /// load balancing logic will be postponed until the next power update tick. It may introduce "flickering".
  /// </remarks>
  protected MechanicalNode MechanicalNode { get; private set; }

  /// <summary>The service to get the power stats and manage the power network.</summary>
  protected SmartPowerService SmartPowerService { get; private set; }

  /// <summary>The place where all components get their dependencies.</summary>
  protected virtual void Awake() {
    MechanicalNode = GetComponentFast<MechanicalNode>();
    _pausableBuilding = GetComponentFast<PausableBuilding>();
    _pausableBuilding.PausedChanged += (_, _) => UpdateStatus();
    //FIXME: Make show status and float icon configurable via settings or descendant class. 
    _shutdownStatus =
        StatusToggle.CreateNormalStatusWithFloatingIcon(ShutdownStatusIcon, _loc.T(PowerShutdownModeLocKey));
    GetComponentFast<StatusSubject>().RegisterStatus(_shutdownStatus);
    enabled = false;
  }

  /// <summary>
  /// Resumes a suspended generator. If the class was inherited, the base method should be called the last in chain.
  /// </summary>
  protected virtual void Resume() {
    HostedDebugLog.Fine(this, "Resume generator: nominalPower={0}, actualPower={1}",
                        MechanicalNode._nominalPowerOutput, MechanicalNode.PowerOutput);
    IsSuspended = false;
    _shutdownStatus.Deactivate();
  }

  /// <summary>
  /// Suspends a generator. If the class was inherited, the base method should be called the first in chain.
  /// </summary>
  protected virtual void Suspend() {
    HostedDebugLog.Fine(this, "Suspend generator: nominalPower={0}, actualPower={1}",
                        MechanicalNode._nominalPowerOutput, MechanicalNode.PowerOutput);
    IsSuspended = true;
    _shutdownStatus.Activate();
  }

  /// <summary>Gets the delays for the suspend and resume actions in ticks.</summary>
  protected abstract void GetActionDelays(out TickDelayedAction resumeAction, out TickDelayedAction suspendAction);

  #endregion

  #region Implementation

  const string ShutdownStatusIcon = "IgorZ/status-icon-standby";
  const string PowerShutdownModeLocKey = "IgorZ.SmartPower.PowerOutputBalancer.SuspendedModeStatus";

  ILoc _loc;
  PausableBuilding _pausableBuilding;
  StatusToggle _shutdownStatus;
  TickDelayedAction _resumeDelayedAction;
  TickDelayedAction _suspendDelayedAction;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc, SmartPowerService smartPowerService) {
    _loc = loc;
    SmartPowerService = smartPowerService;
  }

  void UpdateStatus() {
    if (!enabled) {
      return;
    }
    if (Automate && _pausableBuilding.Paused && IsSuspended) {
      Resume();
    }
    if (Automate && !_pausableBuilding.Paused) {
      HandleSmartLogic();
    }
  }

  void HandleSmartLogic() {
    SmartPowerService.GetBatteriesStat(MechanicalNode.Graph, out var capacity, out var charge);
    var reservedPower = SmartPowerService.GetReservedPower(MechanicalNode.Graph);

    // Keep the batteries charged if there are any. Disregard the supply/demand balance if no suspended consumers.
    if (capacity > 0 && reservedPower == 0) {
      var chargeRatio = charge / capacity;
      if (!IsSuspended && chargeRatio > ChargeBatteriesThreshold - float.Epsilon) {  // >=
        Suspend();
      }
      if (IsSuspended && chargeRatio < DischargeBatteriesThreshold + float.Epsilon) {  // <=
        Resume();
      }
      return;
    }

    var currentPower = MechanicalNode.Graph.CurrentPower;
    var demand = currentPower.PowerDemand + reservedPower;
    var supply = currentPower.PowerSupply;

    // If the generator is suspended, then it should be resumed only if the demand is greater than the supply.
    if (IsSuspended) {
      if (demand > supply) {
        _resumeDelayedAction.Execute(Resume);
      }
      return;
    }

    // Suspend the generator if the supply is greater than the demand.
    if (supply - MechanicalNode.PowerOutput >= demand) {
      if (MechanicalNode.PowerOutput > 0) {
        _suspendDelayedAction.Execute(Suspend);
      } else {
        Suspend();  // Inactive generators don't need hysteresis.
      }
    }
  }

  void SetupDelays() {
    GetActionDelays(out _resumeDelayedAction, out _suspendDelayedAction);
  }

  #endregion
}
