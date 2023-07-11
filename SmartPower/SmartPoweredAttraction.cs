// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.EnterableSystem;
using Timberborn.MechanicalSystem;
using Timberborn.PrefabSystem;
using UnityDev.LogUtils;

namespace SmartPower {

/// <summary>Smart version of teh stock game powered attraction.</summary>
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
      DebugEx.Fine("Disable power consumption on {0}", GetComponentFast<Prefab>().Name);
    } else {
      DebugEx.Fine("Enable power consumption on {0}", GetComponentFast<Prefab>().Name);
    }
    _mechanicalBuilding.ConsumptionDisabled = newState;
  }
}

}
