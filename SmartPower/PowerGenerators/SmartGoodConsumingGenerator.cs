// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.SmartPower.Utils;
using Timberborn.GoodConsumingBuildingSystem;

namespace IgorZ.SmartPower.PowerGenerators;

sealed class SmartGoodConsumingGenerator : PowerOutputBalancer {

  /// <inheritdoc/>
  protected override void GetActionDelays(out TickDelayedAction resumeAction, out TickDelayedAction suspendAction) {
    resumeAction = SmartPowerService.GetTickDelayedAction(0);
    suspendAction = SmartPowerService.GetTickDelayedAction(0);
  }

  /// <inheritdoc/>
  protected override void Suspend() {
    base.Suspend();
    _goodConsumingToggle.PauseConsumption();
    MechanicalNode.UpdateOutput(0);
  }

  /// <inheritdoc/>
  protected override void Resume() {
    _goodConsumingToggle.ResumeConsumption();
    if (_goodConsumingBuilding.HoursUntilNoSupply > 0) {
      MechanicalNode.Active = true;
      MechanicalNode.UpdateOutput(1.0f);
    }
    base.Resume();
  }

  GoodConsumingBuilding _goodConsumingBuilding;
  GoodConsumingToggle _goodConsumingToggle;

  protected override void Awake() {
    base.Awake();
    _goodConsumingBuilding = GetComponentFast<GoodConsumingBuilding>();
    _goodConsumingToggle = _goodConsumingBuilding.GetGoodConsumingToggle();
    Automate = true;
  }
}
