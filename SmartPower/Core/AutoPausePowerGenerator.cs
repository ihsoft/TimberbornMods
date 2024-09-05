// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.BuildingsBlocking;

namespace IgorZ.SmartPower.Core {

/// <summary>Automatically pauses or resumes the building based on the power demand/supply situation.</summary>
/// <remarks>
/// Be wise using this component. If the building has resources, then in the paused state they will be removed.
/// </remarks>
public sealed class AutoPausePowerGenerator : BaseComponent {

  PausableBuilding _pausable;
  PowerOutputBalancer _powerOutputBalancer;

  void Awake() {
    _pausable = GetComponentFast<PausableBuilding>();
    _powerOutputBalancer = GetComponentFast<PowerOutputBalancer>();
  }

  /// <summary>Decides if the building should be paused or resumed based on the power demand/supply situation.</summary>
  public void Decide() {
    var decision = _powerOutputBalancer.GetDecision();
    _powerOutputBalancer.AcceptDecision(decision);
    if (decision == PowerOutputBalancer.Decision.StartMakingPower) {
      _pausable.Resume();
    } else if (decision == PowerOutputBalancer.Decision.StopMakingPower) {
      _pausable.Pause();
    }
  }
}

}
