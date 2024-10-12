// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BuildingsBlocking;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.MechanicalSystem;

namespace IgorZ.SmartPower.PowerGenerators;

sealed class SmartGoodConsumingGenerator : PowerOutputBalancer {
  /// <inheritdoc/>
  public override int Priority => 10;

  /// <inheritdoc/>
  public override void Suspend() {
    base.Suspend();
    _goodConsumingToggle.PauseConsumption();
    _mechanicalNode.UpdateOutput(0);
  }

  /// <inheritdoc/>
  public override void Resume() {
    base.Resume();
    _goodConsumingToggle.ResumeConsumption();
    if (_goodConsumingBuilding.HoursUntilNoSupply > 0) {
      _mechanicalNode.Active = true;
      _mechanicalNode.UpdateOutput(1.0f);
    }
  }

  GoodConsumingBuilding _goodConsumingBuilding;
  GoodConsumingToggle _goodConsumingToggle;
  MechanicalNode _mechanicalNode;
  PausableBuilding _pausable;

  protected override void Awake() {
    base.Awake();
    _goodConsumingBuilding = GetComponentFast<GoodConsumingBuilding>();
    _goodConsumingToggle = _goodConsumingBuilding.GetGoodConsumingToggle();
    _mechanicalNode = GetComponentFast<MechanicalNode>();
    _pausable = GetComponentFast<PausableBuilding>();
    _pausable.PausedChanged += (_, _) => UpdateRegistration();
  }

  protected override void UpdateRegistration() {
    if (Automate && !_pausable.Paused) {
      SmartPowerService.RegisterGenerator(this);
    }
    if (!Automate || _pausable.Paused) {
      if (IsSuspended) {
        Resume();
      }
      SmartPowerService.UnregisterGenerator(this);
    }
  }
}
