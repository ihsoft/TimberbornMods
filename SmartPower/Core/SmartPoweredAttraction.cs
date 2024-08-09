// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.EnterableSystem;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

/// <summary>Smart version of the stock game powered attraction.</summary>
/// <remarks>It only consumes power if there are attendees in the building.</remarks>
public sealed class SmartPoweredAttraction : TickableComponent, IPostInitializableLoadedEntity {

  /// <summary>Indicates if the power consumption has been disabled due to no attendees.</summary>
  // ReSharper disable once MemberCanBePrivate.Global
  public bool ConsumptionDisabled { get; private set; }

  #region IPostInitializableLoadedEntity implementation

  /// <inheritdoc/>
  public void PostInitializeLoadedEntity() {
    UpdateConsumingState();
  }

  #endregion

  #region TickableComponent overrides

  /// <inheritdoc/>
  public override void Tick() {
    // Don't use Enterable events since other components can override the state. This component will be added last, so
    // it will tick the last.
    UpdateConsumingState();
  }

  #endregion

  #region Implementation

  MechanicalBuilding _mechanicalBuilding;
  Enterable _enterable;

  void Awake() {
    _mechanicalBuilding = GetComponentFast<MechanicalBuilding>();
    _enterable = GetComponentFast<Enterable>();
  }

  void UpdateConsumingState() {
    var newState = _enterable.NumberOfEnterersInside == 0;
    if (newState != ConsumptionDisabled) {
      HostedDebugLog.Fine(this, newState ? "Disable power consumption" : "Enable power consumption");
      ConsumptionDisabled = newState;
    }
    _mechanicalBuilding.ConsumptionDisabled = ConsumptionDisabled;
  }

  #endregion
}

}
