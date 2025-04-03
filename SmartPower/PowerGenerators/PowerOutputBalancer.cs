// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.SmartPower.Core;
using IgorZ.SmartPower.Utils;
using IgorZ.TimberDev.Utils;
using Timberborn.BlockSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.EntitySystem;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.StatusSystem;
using Timberborn.TickSystem;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.SmartPower.PowerGenerators;

abstract class PowerOutputBalancer
    : TickableComponent, IPersistentEntity, IFinishedStateListener, IPostInitializableEntity {

  const float MaxBatteryChargeRatio = 0.9f;
  const float MinBatteryChargeRatio = 0.65f;

  #region API

  /// <summary>Indicates the number of minutes till the generator will resume.</summary>
  /// <value>-1 if not applicable in the current state.</value>
  public int MinutesTillResume => IsSuspended && ResumeDelayedAction.IsTicking()
      ? Mathf.RoundToInt(ResumeDelayedAction.TicksLeft * SmartPowerService.FixedDeltaTimeInMinutes)
      : -1;

  /// <summary>Indicates the number of minutes till the generator will suspend.</summary>
  /// <value>-1 if not applicable in the current state.</value>
  public int MinutesTillSuspend => !IsSuspended && SuspendDelayedAction.IsTicking()
      ? Mathf.RoundToInt(SuspendDelayedAction.TicksLeft * SmartPowerService.FixedDeltaTimeInMinutes)
      : -1;

  /// <summary>Indicates if the generator is currently suspended.</summary>
  public bool IsSuspended { get; private set; }

  /// <summary>The minimum level to let the batteries discharge to.</summary>
  /// <seealso cref="UpdateState"/>
  public float DischargeBatteriesThreshold { get; set; } = MinBatteryChargeRatio;

  /// <summary>The maximum level to which this generator should charge the batteries.</summary>
  /// <seealso cref="UpdateState"/>
  public float ChargeBatteriesThreshold { get; set; } = MaxBatteryChargeRatio;

  /// <summary>Indicates whether the generator should automatically pause/unpause based on power demand.</summary>
  /// <seealso cref="UpdateState"/>
  public bool Automate { get; set; }

  /// <summary>Returns the same balancers in the network.</summary>
  public IEnumerable<PowerOutputBalancer> AllBalancers => MechanicalNode.Graph.Nodes
      .Select(x => x.GetComponentFast<PowerOutputBalancer>())
      .Where(x => x != null && x.name == name);

  /// <summary>Updates the suspend state of the consumer to the current power state.</summary>
  /// <remarks>Call it when settings have been changed.</remarks>
  public void UpdateState() {
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

  #endregion

  #region TickableComponent overrides

  /// <inheritdoc/>
  public override void Tick() {
    if (SmartPowerService.SmartLogicStarted) {
      UpdateState();
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
  public void PostInitializeEntity() {
    if (IsSuspended) {
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
    Automate = state.GetValueOrNullable(AutomateKey) ?? Automate;
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

  /// <summary>Indicates if a floating icon should be shown when the generator is suspended.</summary>
  /// <remarks>Must be set before the base `Awake` method is executed.</remarks>
  protected bool ShowFloatingIcon = true;

  /// <summary>Action that is used to resume generator.</summary>
  /// <remarks>If not set before the base `Awake` method is executed, then there will be no delay.</remarks>
  protected TickDelayedAction ResumeDelayedAction;

  /// <summary>Action that is used to resume generator.</summary>
  /// <remarks>If not set before the base `Awake` method is executed, then there will be no delay.</remarks>
  protected TickDelayedAction SuspendDelayedAction;

  /// <summary>The place where all components get their dependencies.</summary>
  protected virtual void Awake() {
    MechanicalNode = GetComponentFast<MechanicalNode>();
    _pausableBuilding = GetComponentFast<PausableBuilding>();
    _pausableBuilding.PausedChanged += (_, _) => UpdateState();

    _shutdownStatus = ShowFloatingIcon
        ? StatusToggle.CreateNormalStatusWithFloatingIcon(ShutdownStatusIcon, _loc.T(PowerShutdownModeLocKey))
        : StatusToggle.CreateNormalStatus(ShutdownStatusIcon, _loc.T(PowerShutdownModeLocKey));
    GetComponentFast<StatusSubject>().RegisterStatus(_shutdownStatus);

    SuspendDelayedAction ??= SmartPowerService.GetTickDelayedAction(0);
    ResumeDelayedAction ??= SmartPowerService.GetTickDelayedAction(0);

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

  #endregion

  #region Implementation

  const string ShutdownStatusIcon = "IgorZ/status-icon-standby";
  const string PowerShutdownModeLocKey = "IgorZ.SmartPower.PowerOutputBalancer.SuspendedModeStatus";

  ILoc _loc;
  PausableBuilding _pausableBuilding;
  StatusToggle _shutdownStatus;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc, SmartPowerService smartPowerService) {
    _loc = loc;
    SmartPowerService = smartPowerService;
  }

  void HandleSmartLogic() {
    SmartPowerService.GetBatteriesStat(MechanicalNode.Graph, out var capacity, out var charge);
    var reservedPower = SmartPowerService.GetReservedPower(MechanicalNode.Graph);

    // Keep the batteries charged if there are any. Disregard the supply/demand balance if no suspended consumers.
    if (capacity > 0 && reservedPower == 0) {
      var chargeRatio = charge / capacity;
      if (IsSuspended) {
        if (chargeRatio < DischargeBatteriesThreshold + float.Epsilon) {  // <=
          Resume();
        }
      } else {
        if (chargeRatio > ChargeBatteriesThreshold - float.Epsilon) {  // >=
          Suspend();
        }
      }
      ResumeDelayedAction.Reset();
      SuspendDelayedAction.Reset();
      return;
    }

    var currentPower = MechanicalNode.Graph.CurrentPower;
    var demand = currentPower.PowerDemand + reservedPower;
    var supply = currentPower.PowerSupply;

    // If the generator is suspended, then it should be resumed only if the demand is greater than the supply.
    if (IsSuspended) {
      if (demand > supply) {
        SuspendDelayedAction.Reset();
        ResumeDelayedAction.Execute(Resume);
      }
      return;
    }

    // Suspend the generator if the supply is greater than the demand.
    if (supply - MechanicalNode.PowerOutput >= demand) {
      ResumeDelayedAction.Reset();
      // Inactive generators don't need hysteresis.
      SuspendDelayedAction.Execute(Suspend, MechanicalNode.PowerOutput == 0);
    }
  }

  #endregion
}
