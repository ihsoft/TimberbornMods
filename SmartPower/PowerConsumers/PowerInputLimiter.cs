// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.SmartPower.Core;
using IgorZ.SmartPower.Settings;
using IgorZ.SmartPower.Utils;
using Timberborn.Attractions;
using Timberborn.BlockSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.StatusSystem;
using Timberborn.TickSystem;
using Timberborn.Workshops;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.Serialization;

namespace IgorZ.SmartPower.PowerConsumers;

sealed class PowerInputLimiter
    : TickableComponent, IPersistentEntity, IFinishedStateListener, IPostInitializableLoadedEntity {

  #region API

  /// <summary>Indicates if the generator is currently suspended.</summary>
  public bool IsSuspended { get; private set; }

  /// <summary>Returns balancers in the same network.</summary>
  public IEnumerable<PowerInputLimiter> AllLimiters => _mechanicalNode.Graph.Nodes
      .Select(x => x.GetComponentFast<PowerInputLimiter>())
      .Where(x => x != null && x.name == name);

  /// <summary>Tells the consumer should automatically pause/unpause based on the power supply.</summary>
  public bool Automate {
    get => _automate;
    set {
      _automate = value;
      UpdateState();
    }
  }
  bool _automate;

  /// <summary>The minimum network efficiency to keep the consumer working.</summary>
  /// <remarks>This value can change in runtime without notifying SmartPowerSystem.</remarks>
  public float MinPowerEfficiency { get; set; } = 0.9f;

  /// <summary>Tells the consumer should suspend if batteries charge drops below the threshold.</summary>
  /// <remarks>This value can change in runtime without notifying SmartPowerSystem.</remarks>
  /// <seealso cref="MinBatteriesCharge"/>
  public bool CheckBatteryCharge { get; set; }

  /// <summary>The minimum level of the batteries to keep the consumer working.</summary>
  /// <remarks>This value can change in runtime without notifying SmartPowerSystem.</remarks>
  /// <seealso cref="CheckBatteryCharge"/>
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

  #endregion

  #region TickableComponent overrides

  /// <inheritdoc/>
  public override void StartTickable() {
    SetupDelays();
    base.StartTickable();
  }

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
  public void PostInitializeLoadedEntity() {
    if (enabled && IsSuspended) {
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
    if (!entityLoader.HasComponent(PowerInputLimiterKey)) {
      return;
    }
    var state = entityLoader.GetComponent(PowerInputLimiterKey);
    _automate = state.GetValueOrNullable(AutomateKey) ?? false;
    MinPowerEfficiency = state.GetValueOrNullable(MinPowerEfficiencyKey) ?? MinPowerEfficiency;
    CheckBatteryCharge = state.GetValueOrNullable(CheckBatteryChargeKey) ?? false;
    MinBatteriesCharge = state.GetValueOrNullable(MinBatteriesChargeKey) ?? MinBatteriesCharge;
    IsSuspended = state.GetValueOrNullable(IsSuspendedKey) ?? false;
  }

  #endregion

  #region Implementation

  const string ShutdownStatusIcon = "IgorZ/status-icon-standby";
  const string PowerShutdownModeLocKey = "IgorZ.SmartPower.PowerInputLimiter.PowerShutdownModeStatus";

  ILoc _loc;
  SmartPowerService _smartPowerService;

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
  public void InjectDependencies(ILoc loc, SmartPowerService smartPowerService) {
    _loc = loc;
    _smartPowerService = smartPowerService;
  }

  void Awake() {
    _mechanicalNode = GetComponentFast<MechanicalNode>();
    _nominalPowerInput = GetComponentFast<MechanicalNodeSpecification>().PowerInput;
    _desiredPower = _nominalPowerInput;
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _pausableBuilding = GetComponentFast<PausableBuilding>();
    _pausableBuilding.PausedChanged += (_, _) => UpdateState();
    //FIXME: Control via settings.
    _shutdownStatus =
        StatusToggle.CreateNormalStatusWithFloatingIcon(ShutdownStatusIcon, _loc.T(PowerShutdownModeLocKey));
    GetComponentFast<StatusSubject>().RegisterStatus(_shutdownStatus);

    enabled = false;
  }

  void UpdateState() {
    if (IsSuspended && !SmartLogicActive) {
      Resume();
    }
    if (SmartLogicActive) {
      HandleSmartLogic();
    }
  }

  void HandleSmartLogic() {
    _smartPowerService.GetBatteriesStat(_mechanicalNode.Graph, out var capacity, out var charge);

    // Shutdown if batteries are low (and present in the network).
    if (CheckBatteryCharge && capacity > 0) {
      var chargeRatio = charge / capacity;
      if (chargeRatio < MinBatteriesCharge) {
        if (!IsSuspended) {
          Suspend(); // Suspend to not drain batteries.
        }
        return;
      }
    }

    var currentPower = _mechanicalNode.Graph.CurrentPower;
    var demand = (float) currentPower.PowerDemand;
    var supply = (float) currentPower.PowerSupply;

    // Resume if power efficiency has improved.
    if (IsSuspended) {
      var newFlow = supply - demand - _desiredPower;
      var estimatedCharge = charge + newFlow * NetworkUISettings.BatteryRatioHysteresis;
      if (CheckBatteryCharge && capacity > 0 && estimatedCharge / capacity < MinBatteriesCharge) {
        return; // If resumed, batteries will be drained too fast (a hysteresis check).
      }
      var noBatteryEfficiency = supply / (demand + _desiredPower);
      if (noBatteryEfficiency < MinPowerEfficiency && (capacity == 0 || estimatedCharge < 0)) {
        return; // No batteries and not enough supply.
      }
      _resumeDelayedAction.Execute(Resume);
      return;
    }

    // Suspend if power efficiency has dropped.
    if (currentPower.PowerEfficiency < MinPowerEfficiency) {
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

  // FIXME: Get delays from settings
  void SetupDelays() {
    var manufactory = GetComponentFast<Manufactory>();
    if (manufactory
        && (manufactory.ProductionRecipes.Length > 1
            || manufactory.ProductionRecipes[0].Ingredients.Count > 0
            || manufactory.ProductionRecipes[0].ConsumesFuel)) {
      _suspendDelayedAction = _smartPowerService.GetTimeDelayedAction(60);
      _resumeDelayedAction = _smartPowerService.GetTimeDelayedAction(60);
      return;
    }

    if (GetComponentFast<Attraction>()) {
      _suspendDelayedAction = _smartPowerService.GetTimeDelayedAction(60);
      _resumeDelayedAction = _smartPowerService.GetTimeDelayedAction(30);
      return;
    }

    _suspendDelayedAction = _smartPowerService.GetTickDelayedAction(1);
    _resumeDelayedAction = _smartPowerService.GetTickDelayedAction(1);
  }

  #endregion
}
