// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.EnterableSystem;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.StatusSystem;
using Timberborn.Workshops;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

/// <summary>
/// Component that extends the stock <see cref="MechanicalBuilding"/> behavior to lower power consumption when the
/// building is not actually making any product.
/// </summary>
/// <remarks>
/// When the building cannot produce product, then the building goes into <see cref="StandbyMode"/>. In this mode it
/// consumes only a fraction of the nominal power.
/// </remarks>
public class SmartMechanicalBuilding : MechanicalBuilding {

  #region API

  /// <summary>Indicates that this building logic must is handled by the smart behavior.</summary>
  // ReSharper disable once MemberCanBePrivate.Global
  public bool NeedsSmartLogic => _mechanicalNode.IsConsumer && !_mechanicalNode.IsGenerator;

  // ReSharper disable once MemberCanBePrivate.Global
  /// <summary>Indicates that teh building has a working place that should have workers.</summary>
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

  #region TickableComponent overrides

  /// <inheritdoc/>
  public override void StartTickable() {
    base.StartTickable();
    if (NeedsSmartLogic) {
      SmartUpdateNodeCharacteristics();
    }
  }

  /// <inheritdoc/>
  public override void Tick() {
    if (!NeedsSmartLogic) {
      base.Tick();
      return;
    }
    SmartUpdateNodeCharacteristics();
    UpdateActiveAndPowered();
  }

  #endregion

  #region Implementation

  const string StandbyStatusIcon = "igorz.smartpower/ui_icons/status-icon-standby";
  const float NonFuelRecipeIdleStateConsumption = 0.1f;
  const string PowerSavingModeLocKey = "IgorZ.SmartPower.MechanicalBuilding.PowerSavingModeStatus";

  Manufactory _manufactory;
  Enterable _enterable;
  ILoc _loc;
  StatusToggle _standbyStatus;

  /// <inheritdoc cref="MechanicalBuilding.Awake" />
  public new void Awake() {
    base.Awake();
    _manufactory = GetComponentFast<Manufactory>();
    _enterable = GetComponentFast<Enterable>();
    _standbyStatus = StatusToggle.CreateNormalStatus(StandbyStatusIcon, _loc.T(PowerSavingModeLocKey));
    var subject = GetComponentFast<StatusSubject>();
    subject.RegisterStatus(_standbyStatus);
    HasWorkingPlaces = GetComponentFast<Workshop>() != null;
  }

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc) {
    _loc = loc;
  }

  void SmartUpdateNodeCharacteristics() {
    if (ConsumptionDisabled) {
      _mechanicalNode.UpdateInput(0);
      AllWorkersOut = MissingIngredients = BlockedOutput = NoFuel = StandbyMode = false;
      return;
    }
    var hasRecipe = _manufactory.HasCurrentRecipe;
    AllWorkersOut = HasWorkingPlaces && hasRecipe && _enterable != null && _enterable.NumberOfEnterersInside == 0;
    MissingIngredients = hasRecipe && !_manufactory.HasAllIngredients;
    BlockedOutput = hasRecipe && !_manufactory.Inventory.HasUnreservedCapacity(_manufactory.CurrentRecipe.Products);
    NoFuel = hasRecipe && !_manufactory.HasFuel;
    StandbyMode = AllWorkersOut || MissingIngredients || NoFuel || BlockedOutput;
    _mechanicalNode.UpdateInput(StandbyMode ? NonFuelRecipeIdleStateConsumption : 1.0f);
  }

  #endregion
}

}
