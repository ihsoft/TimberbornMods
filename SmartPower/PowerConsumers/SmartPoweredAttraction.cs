// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.SmartPower.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.EnterableSystem;
using Timberborn.MechanicalSystem;

namespace IgorZ.SmartPower.PowerConsumers;

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
      if (_powerInputLimiter) {
        _powerInputLimiter.SetDesiredPower(-1);
      }
      return 0;
    }
    var newInput = _enterable.NumberOfEnterersInside == 0 ? NoAttendeesPowerConsumption : _nominalPowerInput;
    if (_powerInputLimiter) {
      _powerInputLimiter.SetDesiredPower(newInput);
    }
    return newInput;
  }

  #endregion

  #region Implementation

  MechanicalBuilding _mechanicalBuilding;
  BlockableBuilding _blockableBuilding;
  Enterable _enterable;
  PowerInputLimiter _powerInputLimiter;

  int _nominalPowerInput;

  void Awake() {
    _mechanicalBuilding = GetComponentFast<MechanicalBuilding>();
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _enterable = GetComponentFast<Enterable>();
    _powerInputLimiter = GetComponentFast<PowerInputLimiter>();
    _nominalPowerInput = GetComponentFast<MechanicalNodeSpecification>().PowerInput;
  }

  #endregion
}
