// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.EnterableSystem;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.StatusSystem;
using Timberborn.Workshops;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

/// <summary>
/// Component that extends the <see cref="MechanicalBuilding"/> behavior to conserve energy when manufactory cannot
/// produce product.
/// </summary>
public class SmartManufactory : BaseComponent, IAdjustablePowerInput {

  #region API

  // ReSharper disable once MemberCanBePrivate.Global
  /// <summary>Indicates that the building has a working place that should have workers.</summary>
  public bool HasWorkingPlaces { get; private set; }

  /// <summary>
  /// Indicates that the building is expected to be staffed and working, but no workers are currently at the working
  /// place(s).
  /// </summary>
  public bool AllWorkersOut { get; private set; }

  /// <summary>Indicates that the required ingredients are missing and the work cannot start.</summary>
  public bool MissingIngredients { get; private set; }

  /// <summary>Indicates that there is now fuel to execute the recipe.</summary>
  public bool NoFuel { get; private set; }

  /// <summary>Indicates that there is no free space in inventory to stock the product(s).</summary>
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
  public int UpdateAndGetPowerInput(int nominalPowerInput) {
    if (_mechanicalBuilding.ConsumptionDisabled || _blockableBuilding && !_blockableBuilding.IsUnblocked) {
      AllWorkersOut = MissingIngredients = BlockedOutput = NoFuel = StandbyMode = false;
      return 0;
    }

    AllWorkersOut = HasWorkingPlaces && _enterable.NumberOfEnterersInside == 0;
    MissingIngredients = !_manufactory.HasAllIngredients;
    BlockedOutput = !_manufactory.Inventory.HasUnreservedCapacity(_manufactory.CurrentRecipe.Products);
    NoFuel = !_manufactory.HasFuel;
    StandbyMode = AllWorkersOut || MissingIngredients || NoFuel || BlockedOutput;
    var newInput = nominalPowerInput * (StandbyMode ? NonFuelRecipeIdleStateConsumption : 1f);
    return Mathf.Max(Mathf.RoundToInt(newInput), 1);
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
  StatusToggle _standbyStatus;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc) {
    _loc = loc;
  }

  void Awake() {
    _mechanicalBuilding = GetComponentFast<MechanicalBuilding>();
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _manufactory = GetComponentFast<Manufactory>();
    _enterable = GetComponentFast<Enterable>();
    _standbyStatus = StatusToggle.CreateNormalStatus(StandbyStatusIcon, _loc.T(PowerSavingModeLocKey));
    var subject = GetComponentFast<StatusSubject>();
    subject.RegisterStatus(_standbyStatus);
    HasWorkingPlaces = GetComponentFast<Workshop>() != null;
  }

  #endregion
}

}
