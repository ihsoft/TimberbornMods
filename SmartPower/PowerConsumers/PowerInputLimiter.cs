// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.SmartPower.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.StatusSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.SmartPower.PowerConsumers;

sealed class PowerInputLimiter : BaseComponent, ISuspendableConsumer, IPersistentEntity, IFinishedStateListener, IPostInitializableLoadedEntity {
  const string ShutdownStatusIcon = "IgorZ/status-icon-standby";
  const string PowerShutdownModeLocKey = "IgorZ.SmartPower.PowerInputLimiter.PowerShutdownModeStatus";

  #region Unity conrolled fields
  // ReSharper disable InconsistentNaming
  // ReSharper disable RedundantDefaultMemberInitializer

  /// <summary>Tells if the generator should be automatically enrolled to be automated once created.</summary>
  [SerializeField]
  [Tooltip("Consumers with higher priority will be resumed first and suspended last.")]
  internal int _priority = 0;

  // ReSharper restore InconsistentNaming
  // ReSharper restore RedundantDefaultMemberInitializer
  #endregion

  #region API

  /// <summary>Returns balancers in the same network.</summary>
  public IEnumerable<PowerInputLimiter> AllLimiters => MechanicalNode.Graph.Nodes
      .Select(x => x.GetComponentFast<PowerInputLimiter>())
      .Where(x => x != null && x.name == name);

  /// <summary>Tells the consumer should automatically pause/unpause based on the power supply.</summary>
  public bool Automate {
    get => _automate;
    set {
      _automate = value;
      UpdateRegistration();
    }
  }
  bool _automate;

  /// <inheritdoc/>
  public bool CheckBatteryCharge { get; set; }

  /// <inheritdoc/>
  public int Priority => _priority;

  /// <inheritdoc/>
  public MechanicalNode MechanicalNode { get; private set; }

  /// <inheritdoc/>
  public int DesiredPower { get; private set; }

  /// <inheritdoc/>
  public bool IsSuspended { get; private set; }

  /// <inheritdoc/>
  public float MinPowerEfficiency { get; set; } = 0.9f;

  /// <inheritdoc/>
  public float MinBatteriesCharge { get; set; } = 0.3f;

  /// <inheritdoc/>
  public void Suspend(bool forceStop) {
    IsSuspended = true;
    _blockableBuilding.Block(this);
    _shutdownStatus.Activate();
  }

  /// <inheritdoc/>
  public void Resume() {
    IsSuspended = false;
    _blockableBuilding.Unblock(this);
    _shutdownStatus.Deactivate();
  }

  /// <inheritdoc/>
  public void OverrideValues(int? power=null) {
    if (power.HasValue && power.Value != DesiredPower) {
      var newValue = power.Value >= 0 ? power.Value : _nominalPowerInput;
      if (newValue != DesiredPower) {
        HostedDebugLog.Fine(this, "Override power: {0} -> {1}", DesiredPower, newValue);
        DesiredPower = newValue;
        _smartPowerService.UpdateConsumerOverrides(this);
      }
    }
  }

  #endregion

  #region IComparable implementation

  public int CompareTo(ISuspendableConsumer other) {
    var priorityCheck = Priority.CompareTo(other.Priority);
    if (priorityCheck != 0) {
      return priorityCheck;
    }
    // Reverse check the power to have the highest power consumers disabled first.
    var powerCheck = other.DesiredPower.CompareTo(DesiredPower);
    return powerCheck == 0 ? GetHashCode().CompareTo(other.GetHashCode()) : powerCheck;
  }

  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    enabled = true;
    UpdateRegistration();
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    enabled = false;
    _smartPowerService.UnregisterConsumer(this);
  }

  #endregion

  #region IPostInitializableLoadedEntity implementation

  public void PostInitializeLoadedEntity() {
    if (enabled && IsSuspended) {
      Suspend(true);
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

  ILoc _loc;
  BlockableBuilding _blockableBuilding;
  PausableBuilding _pausableBuilding;
  StatusToggle _shutdownStatus;
  SmartPowerService _smartPowerService;

  int _nominalPowerInput;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc, SmartPowerService smartPowerService) {
    _loc = loc;
    _smartPowerService = smartPowerService;
  }

  void Awake() {
    MechanicalNode = GetComponentFast<MechanicalNode>();
    _nominalPowerInput = GetComponentFast<MechanicalNodeSpecification>().PowerInput;
    DesiredPower = _nominalPowerInput;
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _pausableBuilding = GetComponentFast<PausableBuilding>();
    _pausableBuilding.PausedChanged += (_, _) => UpdateRegistration();
    _shutdownStatus =
        StatusToggle.CreateNormalStatusWithFloatingIcon(ShutdownStatusIcon, _loc.T(PowerShutdownModeLocKey));
    GetComponentFast<StatusSubject>().RegisterStatus(_shutdownStatus);
    enabled = false;
  }

  void UpdateRegistration() {
    if (!enabled) {
      return;
    }
    if (_automate && !_pausableBuilding.Paused && !_smartPowerService.IsConsumerRegistered(this)) {
      _smartPowerService.RegisterConsumer(this);
    }
    if ((!_automate || _pausableBuilding.Paused) && _smartPowerService.IsConsumerRegistered(this)) {
      if (IsSuspended) {
        Resume();
      }
      _smartPowerService.UnregisterConsumer(this);
    }
  }

  #endregion
}
