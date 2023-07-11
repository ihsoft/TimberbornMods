// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.MechanicalSystem;
using Timberborn.PowerGenerating;
using Timberborn.PrefabSystem;
using UnityDev.LogUtils;
using UnityDev.Utils.ReflectionUtils;

namespace SmartPower {

/// <summary>Smart version of the stock power generator.</summary>
/// <remarks>
/// It will check the actual demand and supply and only stop/start as many generators as needed to satisfy the demand.
/// The checking algorithm doesn't take into account the power in the batteries.
/// </remarks>
public sealed class SmartGoodPoweredGenerator : GoodPoweredGenerator {
  GoodConsumingBuilding _goodConsumingBuilding;
  MechanicalNode _mechanicalNode;
  int _maxPower;
  int _skipTicks;
  static readonly ReflectedField<MechanicalNode, int> NominalPowerOutputField = new("_nominalPowerOutput");

  #region TickableComponent implementation
  /// <inheritdoc/>
  public override void Tick() {
    if (_mechanicalNode.Graph != null) {
      UpdateGoodConsumption();
    }
  }
  #endregion

  #region Implementation
  new void Awake() {
    base.Awake();
    _goodConsumingBuilding = GetComponentFast<GoodConsumingBuilding>();
    _mechanicalNode = GetComponentFast<MechanicalNode>();
    _maxPower = GetComponentFast<MechanicalNodeSpecification>().PowerOutput;
    enabled = true;
  }

  void UpdateGoodConsumption() {
    if (_skipTicks > 0) {
      --_skipTicks;
      return;
    }
    var currentPower = _mechanicalNode.Graph.CurrentPower;
    var demand = currentPower.PowerDemand;
    var supply = currentPower.PowerSupply;
    if (_goodConsumingBuilding.ConsumptionPaused) {
      if (demand <= supply) {
        return;
      }
      DebugEx.Fine("Start good consumption on {0}: demand={1}, supply={2}",
                   GetComponentFast<Prefab>().Name, demand, supply);
      _goodConsumingBuilding.ResumeConsumption();
      _mechanicalNode.Active = true;
      _mechanicalNode.UpdateOutput(1.0f); // Be optimistic, let it update in the next tick.
      _skipTicks = 1;
    } else {
      if (demand > supply - _maxPower) {
        return;
      }
      DebugEx.Fine("Stop good consumption on {0}: demand={1}, supply={2}",
                   GetComponentFast<Prefab>().Name, demand, supply);
      _goodConsumingBuilding.PauseConsumption();
      _mechanicalNode.UpdateOutput(0);  // The graph will be updated on the next tick.
      _skipTicks = 1;
    }
  }
  #endregion
}

}
