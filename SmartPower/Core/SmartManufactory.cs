// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.SmartPower.PowerConsumers;
using Timberborn.BaseComponentSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.EnterableSystem;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.StatusSystem;
using Timberborn.Workshops;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.SmartPower.Core;

/// <summary>
/// Component that extends the <see cref="MechanicalBuilding"/> behavior to conserve energy when manufactory can't
/// produce product.
/// </summary>
public class SmartManufactory : BaseComponent, IAdjustablePowerInput {

  #region API

  // ReSharper disable once MemberCanBePrivate.Global
  /// <summary>Indicates that the building has a working place that should have workers.</summary>
  public bool HasWorkingPlaces { get; private set; }

  /// <summary>
  /// Indicates that the building is expected to be staffed and working, but no workers are currently at the working
  /// place.
  /// </summary>
  public bool AllWorkersOut { get; private set; }

  /// <summary>Indicates that the required ingredients are missing and the work can't start.</summary>
  public bool MissingIngredients { get; private set; }

  /// <summary>Indicates that there is now fuel to execute the recipe.</summary>
  public bool NoFuel { get; private set; }

  /// <summary>Indicates that there is no free space in inventory to stock the product.</summary>
  public bool BlockedOutput { get; private set; }

  /// <summary>Tells if the building is not consuming full power due to the conditions.</summary>
  public bool StandbyMode {
    get => _idleState;
    private set {
      if (value == _idleState) {
        return;
      }
      _idleState = value;
      HostedDebugLog.Fine(
        this, "Change power consumption mode: noWorkers={0}, noIngredients={1}, noFuel={2}, blockedOutput={3}",
        AllWorkersOut, MissingIngredients, NoFuel, BlockedOutput);
      if (value) {
        _standbyStatus.Activate();
      } else {
        _standbyStatus.Deactivate();
      }
    }
  }
  bool _idleState;

  #endregion

  #region IAdjustablePowerInput implementation

  /// <inheritdoc/>
  public int UpdateAndGetPowerInput() {
    if (_mechanicalBuilding.ConsumptionDisabled
        || _blockableBuilding && !_blockableBuilding.IsUnblocked
        || !_manufactory.HasCurrentRecipe) {
      AllWorkersOut = MissingIngredients = BlockedOutput = NoFuel = StandbyMode = false;
      if (_powerInputLimiter) {
        _powerInputLimiter.SetDesiredPower(-1);
      }
      return 0;
    }

    AllWorkersOut = HasWorkingPlaces && _enterable.NumberOfEnterersInside == 0;
    MissingIngredients = !_manufactory.HasAllIngredients;
    BlockedOutput = !_manufactory.Inventory.HasUnreservedCapacity(_manufactory.CurrentRecipe.Products);
    NoFuel = !_manufactory.HasFuel;
    StandbyMode = AllWorkersOut || MissingIngredients || NoFuel || BlockedOutput;
    var newInput = Math.Max(
        Mathf.RoundToInt(_nominalPowerInput * (StandbyMode ? NonFuelRecipeIdleStateConsumption : 1f)), 1);
    if (_powerInputLimiter) {
      _powerInputLimiter.SetDesiredPower(newInput);
    }
    return newInput;
  }

  #endregion

  #region Implementation

  const string StandbyStatusIcon = "IgorZ/status-icon-standby";
  const float NonFuelRecipeIdleStateConsumption = 0.1f;
  const string PowerSavingModeLocKey = "IgorZ.SmartPower.MechanicalBuilding.PowerSavingModeStatus";

  ILoc _loc;
  MechanicalBuilding _mechanicalBuilding;
  BlockableBuilding _blockableBuilding;
  Manufactory _manufactory;
  Enterable _enterable;
  PowerInputLimiter _powerInputLimiter;
  StatusToggle _standbyStatus;

  int _nominalPowerInput;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc) {
    _loc = loc;
  }

  void Awake() {
    _mechanicalBuilding = GetComponentFast<MechanicalBuilding>();
    _nominalPowerInput = GetComponentFast<MechanicalNodeSpecification>().PowerInput;
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _manufactory = GetComponentFast<Manufactory>();
    _enterable = GetComponentFast<Enterable>();
    _powerInputLimiter = GetComponentFast<PowerInputLimiter>();
    _standbyStatus = StatusToggle.CreateNormalStatus(StandbyStatusIcon, _loc.T(PowerSavingModeLocKey));
    var subject = GetComponentFast<StatusSubject>();
    subject.RegisterStatus(_standbyStatus);
    HasWorkingPlaces = GetComponentFast<Workshop>() != null;
  }

  #endregion
}
