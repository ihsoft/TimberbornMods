// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.EnterableSystem;
using Timberborn.MechanicalSystem;

namespace IgorZ.SmartPower.Core {

/// <summary>
/// Component that extends the <see cref="MechanicalBuilding"/> behavior to conserve energy when powered attraction has
/// no attendees.
/// </summary>
public sealed class SmartPoweredAttraction : BaseComponent, IAdjustablePowerInput {

  #region IAdjustablePowerInput implementation

  // Don't set it to 0 as it can disable the network.
  const int NonAttendeesPowerConsumption = 1;  // hp

  /// <inheritdoc/>
  public int UpdateAndGetPowerInput(int nominalPowerInput) {
    if (_mechanicalBuilding.ConsumptionDisabled || _blockableBuilding && !_blockableBuilding.IsUnblocked) {
      return 0;
    }
    return _enterable.NumberOfEnterersInside == 0 ? NonAttendeesPowerConsumption : nominalPowerInput;
  }

  #endregion

  #region Implementation

  MechanicalBuilding _mechanicalBuilding;
  BlockableBuilding _blockableBuilding;
  Enterable _enterable;

  void Awake() {
    _mechanicalBuilding = GetComponentFast<MechanicalBuilding>();
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _enterable = GetComponentFast<Enterable>();
  }

  #endregion
}

}
