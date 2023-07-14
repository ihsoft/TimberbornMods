// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.EnterableSystem;
using Timberborn.MechanicalSystem;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

/// <summary>Smart version of the stock game powered attraction.</summary>
/// <remarks>It only consumes power if there are attendees in the building.</remarks>
public sealed class SmartPoweredAttraction : BaseComponent {
  Enterable _enterable;
  MechanicalBuilding _mechanicalBuilding;
  
  public void Awake() {
    _mechanicalBuilding = GetComponentFast<MechanicalBuilding>();
    _enterable = GetComponentFast<Enterable>();
    _enterable.EntererAdded += (_, _) => UpdateConsumingState();
    _enterable.EntererRemoved += (_, _) => UpdateConsumingState();
  }

  public void Start() {
    _mechanicalBuilding.ConsumptionDisabled = true;
  }

  void UpdateConsumingState() {
    var newState = _enterable.NumberOfEnterersInside == 0;
    if (newState == _mechanicalBuilding.ConsumptionDisabled) {
      return;
    }
    if (newState) {
      HostedDebugLog.Fine(this, "Disable power consumption");
    } else {
      HostedDebugLog.Fine(this, "Enable power consumption");
    }
    _mechanicalBuilding.ConsumptionDisabled = newState;
  }
}

}
