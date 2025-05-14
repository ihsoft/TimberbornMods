// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.SmartPower.Core;
using IgorZ.SmartPower.Settings;
using IgorZ.SmartPower.Utils;
using IgorZ.TimberDev.Utils;
using Timberborn.Attractions;
using Timberborn.BlockSystem;
using Timberborn.BuildingsBlocking;
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
    : TickableComponent, IPersistentEntity, IFinishedStateListener, IPostInitializableEntity {

  #region API

  /// <summary>Indicates if the generator is currently suspended.</summary>
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
      .Select(x => x.GetComponentFast<PowerInputLimiter>())
      .Where(x => x != null && x.name == name);

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
    Automate = state.GetValueOrDefault(AutomateKey);
    MinPowerEfficiency = state.GetValueOrDefault(MinPowerEfficiencyKey, MinPowerEfficiency);
    CheckBatteryCharge = state.GetValueOrDefault(CheckBatteryChargeKey);
    MinBatteriesCharge = state.GetValueOrDefault(MinBatteriesChargeKey, MinBatteriesCharge);
    IsSuspended = state.GetValueOrDefault(IsSuspendedKey);
  }

  #endregion

  #region Implementation

  const string ShutdownStatusIcon = "IgorZ/status-icon-standby";
  const string PowerShutdownModeLocKey = "IgorZ.SmartPower.PowerInputLimiter.PowerShutdownModeStatus";

  ILoc _loc;
  SmartPowerService _smartPowerService;
  AttractionConsumerSettings _attractionConsumerSettings;
  UnmannedConsumerSettings _unmannedConsumerSettings;
  WorkplaceConsumerSettings _workplaceConsumerSettings;

  BlockableBuilding _blockableBuilding;
  PausableBuilding _pausableBuilding;
  StatusToggle _shutdownStatus;

  int _nominalPowerInput;
  MechanicalNode _mechanicalNode;
  TickDelayedAction _resumeDelayedAction;
  TickDelayedAction _suspendDelayedAction;

  int _desiredPower;

  bool SmartLogicActive => enabled && Automate && !_pausableBuilding.Paused;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc, SmartPowerService smartPowerService,
                                 AttractionConsumerSettings attractionConsumerSettings,
                                 UnmannedConsumerSettings unmannedConsumerSettings,
                                 WorkplaceConsumerSettings workplaceConsumerSettings) {
    _loc = loc;
    _smartPowerService = smartPowerService;
    _attractionConsumerSettings = attractionConsumerSettings;
    _unmannedConsumerSettings = unmannedConsumerSettings;
    _workplaceConsumerSettings = workplaceConsumerSettings;
  }

  void Awake() {
    _mechanicalNode = GetComponentFast<MechanicalNode>();
    _nominalPowerInput = GetComponentFast<MechanicalNodeSpec>().PowerInput;
    _desiredPower = _nominalPowerInput;
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _pausableBuilding = GetComponentFast<PausableBuilding>();
    _pausableBuilding.PausedChanged += (_, _) => UpdateState();
    
    bool showFloatingIcon;
    if (GetComponentFast<Attraction>()) {
      showFloatingIcon = _attractionConsumerSettings.ShowFloatingIcon.Value;
      _suspendDelayedAction =
          _smartPowerService.GetTimeDelayedAction(_attractionConsumerSettings.SuspendDelayMinutes.Value);
      _resumeDelayedAction =
          _smartPowerService.GetTimeDelayedAction(_attractionConsumerSettings.ResumeDelayMinutes.Value);
    } else if (GetComponentFast<Workplace>()) {
      showFloatingIcon = _workplaceConsumerSettings.ShowFloatingIcon.Value;
      _suspendDelayedAction =
          _smartPowerService.GetTimeDelayedAction(_workplaceConsumerSettings.SuspendDelayMinutes.Value);
      _resumeDelayedAction =
          _smartPowerService.GetTimeDelayedAction(_workplaceConsumerSettings.ResumeDelayMinutes.Value);
    } else {
      showFloatingIcon = _unmannedConsumerSettings.ShowFloatingIcon.Value;
      _suspendDelayedAction = _smartPowerService.GetTickDelayedAction(1);
      _resumeDelayedAction = _smartPowerService.GetTickDelayedAction(1);
    }
    
    _shutdownStatus = showFloatingIcon
        ? StatusToggle.CreateNormalStatusWithFloatingIcon(ShutdownStatusIcon, _loc.T(PowerShutdownModeLocKey))
        : StatusToggle.CreateNormalStatus(ShutdownStatusIcon, _loc.T(PowerShutdownModeLocKey));
    GetComponentFast<StatusSubject>().RegisterStatus(_shutdownStatus);

    enabled = false;
  }

  void HandleSmartLogic() {
    _smartPowerService.GetBatteriesStat(_mechanicalNode.Graph, out var capacity, out var charge);

    // Shutdown if batteries are low (and present in the network).
    if (CheckBatteryCharge && capacity > 0 && charge / capacity < MinBatteriesCharge) {
      LowBatteriesCharge = true;
      if (!IsSuspended) {
        _suspendDelayedAction.Execute(Suspend, true); // Immediately suspend to not drain batteries.
      }
      _resumeDelayedAction.Reset();
      return;
    }
    LowBatteriesCharge = false;

    var currentPower = _mechanicalNode.Graph.CurrentPower;
    var demand = (float) currentPower.PowerDemand;
    var supply = (float) currentPower.PowerSupply;

    // Resume if power efficiency has improved.
    if (IsSuspended) {
      var newFlow = supply - demand - _desiredPower;
      var estimatedCharge = charge + newFlow * BatteriesSettings.BatteryRatioHysteresis;
      if (CheckBatteryCharge && capacity > 0 && estimatedCharge / capacity < MinBatteriesCharge) {
        return; // If resumed, batteries will be drained too fast (a hysteresis check).
      }
      var noBatteryEfficiency = supply / (demand + _desiredPower);
      if (noBatteryEfficiency < MinPowerEfficiency && (capacity == 0 || estimatedCharge < 0)) {
        return; // No batteries and not enough supply.
      }
      _suspendDelayedAction.Reset();
      _resumeDelayedAction.Execute(Resume);
      return;
    }

    // Suspend if power efficiency has dropped.
    if (currentPower.PowerEfficiency < MinPowerEfficiency) {
      _resumeDelayedAction.Reset();
      _suspendDelayedAction.Execute(Suspend);
    }
  }

  void Suspend() {
    HostedDebugLog.Fine(
        this, "Suspend consumer: currentPower={0}, desiredPower={1}", _mechanicalNode.PowerInput, _desiredPower);
    IsSuspended = true;
    _blockableBuilding.Block(this);
    _shutdownStatus.Activate();
    _smartPowerService.ReservePower(_mechanicalNode, _desiredPower);
  }

  void Resume() {
    HostedDebugLog.Fine(this, "Resume consumer: desiredPower={0}, nominalPower={1}", _desiredPower, _nominalPowerInput);
    IsSuspended = false;
    _blockableBuilding.Unblock(this);
    _shutdownStatus.Deactivate();
    _smartPowerService.ReservePower(_mechanicalNode, -1);
  }

  #endregion
}
