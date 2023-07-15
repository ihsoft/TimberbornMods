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
using UnityDev.Utils.Reflections;

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
  static readonly ReflectedAction<MechanicalBuilding> UpdateActiveAndPoweredMethod = new(
      "UpdateActiveAndPowered", throwOnFailure: true);

  const string StandbyStatusIcon = "igorz.smartpower/ui_icons/status-icon-standby";
  const float NonFuelRecipeIdleStateConsumption = 0.1f;
  const string PowerSavingModeLocKey = "IgorZ.SmartPower.MechanicalBuilding.PowerSavingModeStatus";

  MechanicalNode _mechanicalNode;
  Manufactory _manufactory;
  Enterable _enterable;
  ILoc _loc;
  StatusToggle _standbyStatus;

  #region API
  /// <summary>
  /// 
  /// </summary>
  public bool AllWorkersOut { get; private set; }
  public bool MissingIngredients { get; private set; }
  public bool NoFuel { get; private set; }
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

  #region MechanicalBuilding overrides
  /// <inheritdoc/>
  public override void StartTickable() {
    base.StartTickable();
    _mechanicalNode = GetComponentFast<MechanicalNode>();
    _manufactory = GetComponentFast<Manufactory>();
    _enterable = GetComponentFast<Enterable>();
    _standbyStatus = StatusToggle.CreateNormalStatus(StandbyStatusIcon, _loc.T(PowerSavingModeLocKey));
    var subject = GetComponentFast<StatusSubject>();
    subject.RegisterStatus(_standbyStatus);
    UpdateNodeCharacteristics();
  }

  /// <inheritdoc/>
  public override void Tick() {
    if (!_mechanicalNode.IsConsumer || _mechanicalNode.IsGenerator) {
      base.Tick();
      return;
    }
    UpdateNodeCharacteristics();
    UpdateActiveAndPoweredMethod.Invoke(this);
  }
  #endregion

  #region Implementation
  [Inject]
  public void InjectDependencies(ILoc loc) {
    _loc = loc;
  }

  void UpdateNodeCharacteristics() {
    if (ConsumptionDisabled) {
      _mechanicalNode.UpdateInput(0);
      AllWorkersOut = MissingIngredients = BlockedOutput = NoFuel = StandbyMode = false;
      return;
    }
    var hasRecipe = _manufactory.HasCurrentRecipe;
    AllWorkersOut = hasRecipe && _enterable != null && _enterable.NumberOfEnterersInside == 0;
    MissingIngredients = hasRecipe && !_manufactory.HasAllIngredients;
    BlockedOutput = hasRecipe && !_manufactory.Inventory.HasUnreservedCapacity(_manufactory.CurrentRecipe.Products);
    NoFuel = hasRecipe && !_manufactory.HasFuel;
    StandbyMode = AllWorkersOut || MissingIngredients || NoFuel || BlockedOutput;
    _mechanicalNode.UpdateInput(StandbyMode ? NonFuelRecipeIdleStateConsumption : 1.0f);
  }
  #endregion
}

}
