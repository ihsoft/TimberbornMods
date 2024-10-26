// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.EnterableSystem;
using Timberborn.MechanicalSystem;

namespace IgorZ.SmartPower.Core;

/// <summary>
/// Component that extends the <see cref="MechanicalBuilding"/> behavior to conserve energy when powered attraction has
/// no attendees.
/// </summary>
public sealed class SmartPoweredAttraction : BaseComponent, IAdjustablePowerInput {

  #region IAdjustablePowerInput implementation

  // Don't set it to 0 as it may disable the network.
  const int NoAttendeesPowerConsumption = 1;  // hp

  /// <inheritdoc/>
  public int UpdateAndGetPowerInput() {
    if (_mechanicalBuilding.ConsumptionDisabled || _blockableBuilding && !_blockableBuilding.IsUnblocked) {
      _suspendableConsumer?.OverrideValues(power: -1);
      return 0;
    }
    var newInput = _enterable.NumberOfEnterersInside == 0 ? NoAttendeesPowerConsumption : _nominalPowerInput;
    _suspendableConsumer?.OverrideValues(power: newInput);
    return newInput;
  }

  #endregion

  #region Implementation

  MechanicalBuilding _mechanicalBuilding;
  BlockableBuilding _blockableBuilding;
  Enterable _enterable;
  ISuspendableConsumer _suspendableConsumer;

  int _nominalPowerInput;

  void Awake() {
    _mechanicalBuilding = GetComponentFast<MechanicalBuilding>();
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _enterable = GetComponentFast<Enterable>();
    _suspendableConsumer = GetComponentFast<ISuspendableConsumer>();
    _nominalPowerInput = GetComponentFast<MechanicalNodeSpecification>().PowerInput;
  }

  #endregion
}
