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

namespace IgorZ.SmartPower.PowerConsumers;

sealed class PowerInputLimiter : BaseComponent, ISuspendableConsumer, IPersistentEntity, IFinishedStateListener, IPostInitializableLoadedEntity {
  const string ShutdownStatusIcon = "IgorZ/status-icon-standby";
  const string PowerShutdownModeLocKey = "IgorZ.SmartPower.PowerInputLimiter.PowerShutdownModeStatus";

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
  public int Priority { get; set; }

  /// <inheritdoc/>
  public MechanicalNode MechanicalNode { get; set; }

  /// <inheritdoc/>
  public int DesiredPower { get; set; }

  /// <inheritdoc/>
  public bool IsSuspended { get; private set; }

  /// <summary>The minimum charge ratio for the batteries to be considered in the supply calculations.</summary>
  public float MinBatteriesCharge { get; set; } = 0.3f;

  /// <inheritdoc/>
  public void Suspend() {
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
      Suspend();
    }
  }

  #endregion

  #region IPersistentEntity implemenatation

  static readonly ComponentKey AutomationBehaviorKey = new(typeof(PowerInputLimiter).FullName);
  static readonly PropertyKey<bool> AutomateKey = new("Automate");
  static readonly PropertyKey<float> MinBatteriesChargeKey = new("MinBatteriesCharge");
  static readonly PropertyKey<bool> IsSuspendedKey = new("IsSuspended");

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var saver = entitySaver.GetComponent(AutomationBehaviorKey);
    saver.Set(AutomateKey, Automate);
    saver.Set(MinBatteriesChargeKey, MinBatteriesCharge);
    saver.Set(IsSuspendedKey, IsSuspended);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.HasComponent(AutomationBehaviorKey)) {
      return;
    }
    var state = entityLoader.GetComponent(AutomationBehaviorKey);
    _automate = state.GetValueOrNullable(AutomateKey) ?? false;
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

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc, SmartPowerService smartPowerService) {
    _loc = loc;
    _smartPowerService = smartPowerService;
  }

  void Awake() {
    MechanicalNode = GetComponentFast<MechanicalNode>();
    DesiredPower = GetComponentFast<MechanicalNodeSpecification>().PowerInput;
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _pausableBuilding = GetComponentFast<PausableBuilding>();
    _pausableBuilding.PausedChanged += (_, _) => UpdateRegistration();
    _shutdownStatus =
        StatusToggle.CreateNormalStatusWithFloatingIcon(ShutdownStatusIcon, _loc.T(PowerShutdownModeLocKey));
    GetComponentFast<StatusSubject>().RegisterStatus(_shutdownStatus);
    enabled = false;
  }

  void UpdateRegistration() {
    if (Automate && !_pausableBuilding.Paused && !_smartPowerService.IsConsumerRegistered(this)) {
      _smartPowerService.RegisterConsumer(this);
    }
    if ((!Automate || _pausableBuilding.Paused) && _smartPowerService.IsConsumerRegistered(this)) {
      if (IsSuspended) {
        Resume();
      }
      _smartPowerService.UnregisterConsumer(this);
    }
  }

  #endregion
}
