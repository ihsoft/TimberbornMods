// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.SmartPower.Core;
using IgorZ.SmartPower.Settings;
using IgorZ.SmartPower.Utils;
using IgorZ.TimberDev.Utils;
using Timberborn.Attractions;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockingSystem;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.DuplicationSystem;
using Timberborn.EntitySystem;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.StatusSystem;
using Timberborn.TickSystem;
using Timberborn.WorkSystem;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.SmartPower.PowerConsumers;

sealed class PowerInputLimiter
    : TickableComponent, IAwakableComponent, IPersistentEntity, IFinishedStateListener, IPostInitializableEntity,
      IDuplicable<PowerInputLimiter> {

  #region API

  /// <summary>Indicates if the consumer is currently suspended.</summary>
  public bool IsSuspended { get; private set; }

  /// <summary>Indicates that the consumer was suspended due to low batteries charge in the network.</summary>
  /// <seealso cref="CheckBatteryCharge"/>
  public bool LowBatteriesCharge { get; private set; }

  /// <summary>Indicates the number of minutes till the consumer will resume.</summary>
  /// <value>-1 if not applicable in the current state.</value>
  public int MinutesTillResume => IsSuspended && _resumeDelayedAction.IsTicking()
      ? Mathf.RoundToInt(_resumeDelayedAction.TicksLeft * _smartPowerService.FixedDeltaTimeInMinutes)
      : -1;

  /// <summary>Indicates the number of minutes till the consumer will suspend.</summary>
  /// <value>-1 if not applicable in the current state.</value>
  public int MinutesTillSuspend => !IsSuspended && _suspendDelayedAction.IsTicking()
      ? Mathf.RoundToInt(_suspendDelayedAction.TicksLeft * _smartPowerService.FixedDeltaTimeInMinutes)
      : -1;

  /// <summary>Returns balancers in the same network.</summary>
  public IEnumerable<PowerInputLimiter> AllLimiters => _mechanicalNode.Graph.Nodes
      .Select(x => x.GetComponent<PowerInputLimiter>())
      .Where(x => x != null && x.Name == Name);

  /// <summary>Tells the consumer should automatically pause/unpause based on the power supply.</summary>
  /// <seealso cref="UpdateState"/>
  public bool Automate { get; set; }

  /// <summary>The minimum network efficiency to keep the consumer working.</summary>
  /// <seealso cref="UpdateState"/>
  public float MinPowerEfficiency { get; set; } = 0.9f;

  /// <summary>Tells the consumer should suspend if batteries charge drops below the threshold.</summary>
  /// <seealso cref="MinBatteriesCharge"/>
  /// <seealso cref="UpdateState"/>
  public bool CheckBatteryCharge { get; set; }

  /// <summary>The minimum level of the batteries to keep the consumer working.</summary>
  /// <seealso cref="CheckBatteryCharge"/>
  /// <seealso cref="UpdateState"/>
  public float MinBatteriesCharge { get; set; } = 0.3f;

  /// <summary>
  /// Sets the new desired power of the consumer. If the value is negative, then the nominal power input is used.
  /// </summary>
  public void SetDesiredPower(int power) {
    var newPower = power < 0 ? _nominalPowerInput : power;
    if (_desiredPower == newPower) {
      return;
    }
    _desiredPower = newPower;
    if (IsSuspended) {
      _smartPowerService.ReservePower(_mechanicalNode, _desiredPower);
    }
  }

  /// <summary>Updates the suspend state of the consumer to the current power state.</summary>
  /// <remarks>Call it when settings have been changed.</remarks>
  public void UpdateState() {
    if (IsSuspended && !SmartLogicActive) {
      Resume();
    }
    if (SmartLogicActive) {
      HandleSmartLogic();
    }

    // Update adjusted power input if needed.
    if (_adjustablePowerInput != null) {
      var newInputPower = _adjustablePowerInput.UpdateAndGetPowerInput();
      if (_mechanicalNode.Actuals.PowerInput != newInputPower) {
        HostedDebugLog.Fine(
            this, "Adjusting power input: {0} => {1}", _mechanicalNode.Actuals.PowerInput, newInputPower);
        _mechanicalNode.Actuals.SetPowerInput(newInputPower);
      }
    }
  }

  #endregion

  #region TickableComponent overrides

  /// <inheritdoc/>
  public override void Tick() {
    if (_smartPowerService.SmartLogicStarted) {
      UpdateState();
    }
  }

  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    EnableComponent();
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    DisableComponent();
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

  static readonly ComponentKey PowerInputLimiterKey = new(typeof(PowerInputLimiter).FullName);
  static readonly PropertyKey<bool> AutomateKey = new("Automate");
  static readonly PropertyKey<float> MinPowerEfficiencyKey = new("MinPowerEfficiency");
  static readonly PropertyKey<bool> CheckBatteryChargeKey = new("CheckBatteryCharge");
  static readonly PropertyKey<float> MinBatteriesChargeKey = new("MinBatteriesCharge");
  static readonly PropertyKey<bool> IsSuspendedKey = new("IsSuspended");

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var saver = entitySaver.GetComponent(PowerInputLimiterKey);
    saver.Set(AutomateKey, Automate);
    saver.Set(MinPowerEfficiencyKey, MinPowerEfficiency);
    saver.Set(CheckBatteryChargeKey, CheckBatteryCharge);
    saver.Set(MinBatteriesChargeKey, MinBatteriesCharge);
    saver.Set(IsSuspendedKey, IsSuspended);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.TryGetComponent(PowerInputLimiterKey, out var state)) {
      return;
    }
    Automate = state.GetValueOrDefault(AutomateKey, Automate);
    MinPowerEfficiency = state.GetValueOrDefault(MinPowerEfficiencyKey, MinPowerEfficiency);
    CheckBatteryCharge = state.GetValueOrDefault(CheckBatteryChargeKey, CheckBatteryCharge);
    MinBatteriesCharge = state.GetValueOrDefault(MinBatteriesChargeKey, MinBatteriesCharge);
    IsSuspended = state.GetValueOrDefault(IsSuspendedKey);
  }

  #endregion

  #region IDuplicable implementation.

    /// <inheritdoc/>
  public void DuplicateFrom(PowerInputLimiter source) {
    Automate = source.Automate;
    MinPowerEfficiency = source.MinPowerEfficiency;
    CheckBatteryCharge = source.CheckBatteryCharge;
    MinBatteriesCharge = source.MinBatteriesCharge;
    UpdateState();
  }

  #endregion

  #region Implementation

  const string ShutdownStatusIcon = "IgorZ/status-icon-standby";
  const string PowerShutdownModeLocKey = "IgorZ.SmartPower.PowerInputLimiter.PowerShutdownModeStatus";

  readonly ILoc _loc;
  readonly SmartPowerService _smartPowerService;

  BlockableObject _blockableObject;
  PausableBuilding _pausableBuilding;
  StatusToggle _shutdownStatus;
  IAdjustablePowerInput _adjustablePowerInput;

  int _nominalPowerInput;
  MechanicalNode _mechanicalNode;
  TickDelayedAction _resumeDelayedAction;
  TickDelayedAction _suspendDelayedAction;

  int _desiredPower;

  bool SmartLogicActive => Enabled && Automate && !_pausableBuilding.Paused;

  PowerInputLimiter(ILoc loc, SmartPowerService smartPowerService) {
    _loc = loc;
    _smartPowerService = smartPowerService;
  }

  public void Awake() {
    _mechanicalNode = GetComponent<MechanicalNode>();
    _nominalPowerInput = GetComponent<MechanicalNodeSpec>().PowerInput;
    _desiredPower = _nominalPowerInput;
    _blockableObject = GetComponent<BlockableObject>();
    _pausableBuilding = GetComponent<PausableBuilding>();
    _pausableBuilding.PausedChanged += UpdateStateWhilePaused;
    _adjustablePowerInput = GetComponent<IAdjustablePowerInput>();

    bool showFloatingIcon;
    if (GetComponent<Attraction>()) {
      showFloatingIcon = AttractionConsumerSettings.ShowFloatingIcon;
      _suspendDelayedAction =
          _smartPowerService.GetTimeDelayedAction(AttractionConsumerSettings.SuspendDelayMinutes);
      _resumeDelayedAction =
          _smartPowerService.GetTimeDelayedAction(AttractionConsumerSettings.ResumeDelayMinutes);
    } else if (GetComponent<Workplace>()) {
      showFloatingIcon = WorkplaceConsumerSettings.ShowFloatingIcon;
      _suspendDelayedAction =
          _smartPowerService.GetTimeDelayedAction(WorkplaceConsumerSettings.SuspendDelayMinutes);
      _resumeDelayedAction =
          _smartPowerService.GetTimeDelayedAction(WorkplaceConsumerSettings.ResumeDelayMinutes);
    } else {
      showFloatingIcon = UnmannedConsumerSettings.ShowFloatingIcon;
      _suspendDelayedAction = _smartPowerService.GetTickDelayedAction(1);
      _resumeDelayedAction = _smartPowerService.GetTickDelayedAction(1);
    }
    
    _shutdownStatus = showFloatingIcon
        ? StatusToggle.CreateNormalStatusWithFloatingIcon(ShutdownStatusIcon, _loc.T(PowerShutdownModeLocKey))
        : StatusToggle.CreateNormalStatus(ShutdownStatusIcon, _loc.T(PowerShutdownModeLocKey));
    GetComponent<StatusSubject>().RegisterStatus(_shutdownStatus);

    DisableComponent();
  }

  // Let the normal tick logic updating during the game. On pause, the update has a purely cosmetic effect.
  void UpdateStateWhilePaused(object sender, EventArgs args) {
    if (SmartPowerService.IsGamePaused) {
      UpdateState();
    }
  }

  void HandleSmartLogic() {
    var mechanicalGraph = _mechanicalNode.Graph;

    var batteryCapacity = (float) mechanicalGraph.BatteryCapacity;
    var batteryCharge = (float) mechanicalGraph.BatteryCharge;

    // Shutdown if batteries are low (and present in the network).
    if (CheckBatteryCharge && batteryCapacity > 0 && batteryCharge / batteryCapacity < MinBatteriesCharge) {
      LowBatteriesCharge = true;
      if (!IsSuspended) {
        _suspendDelayedAction.Execute(Suspend, true); // Immediately suspend to not drain batteries.
      }
      _resumeDelayedAction.Reset();
      return;
    }
    LowBatteriesCharge = false;

    var demand = (float) mechanicalGraph.PowerDemand;
    var supply = (float) mechanicalGraph.PowerSupply;

    // Resume if power efficiency has improved.
    if (IsSuspended) {
      var newFlow = supply - demand - _desiredPower;
      var estimatedCharge = batteryCharge + newFlow * BatteriesSettings.BatteryRatioHysteresis;
      if (CheckBatteryCharge && batteryCapacity > 0 && estimatedCharge / batteryCapacity < MinBatteriesCharge) {
        return; // If resumed, batteries will be drained too fast (a hysteresis check).
      }
      var noBatteryEfficiency = supply / (demand + _desiredPower);
      if (noBatteryEfficiency < MinPowerEfficiency && (batteryCapacity == 0 || estimatedCharge < 0)) {
        return; // No batteries and not enough supply.
      }
      _suspendDelayedAction.Reset();
      _resumeDelayedAction.Execute(Resume);
      return;
    }

    // Suspend if power efficiency has dropped and batteries cannot back up.
    if (batteryCapacity == 0f && mechanicalGraph.PowerEfficiency < MinPowerEfficiency) {
      _resumeDelayedAction.Reset();
      _suspendDelayedAction.Execute(Suspend);
    }
  }

  void Suspend() {
    HostedDebugLog.Fine(this, "Suspend consumer: currentPower={0}, desiredPower={1}",
                        _mechanicalNode.Actuals.PowerInput, _desiredPower);
    IsSuspended = true;
    _shutdownStatus.Activate();
    _smartPowerService.ReservePower(_mechanicalNode, _desiredPower);
    _blockableObject.Block(this);
  }

  void Resume() {
    HostedDebugLog.Fine(this, "Resume consumer: desiredPower={0}, nominalPower={1}", _desiredPower, _nominalPowerInput);
    IsSuspended = false;
    _shutdownStatus.Deactivate();
    _smartPowerService.ReservePower(_mechanicalNode, -1);
    _blockableObject.Unblock(this);
  }

  #endregion
}
