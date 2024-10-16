// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BuildingsBlocking;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.MechanicalSystem;
using UnityDev.Utils.LogUtilsLite;

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
  PausableBuilding _pausableBuilding;

  protected override void Awake() {
    base.Awake();
    _goodConsumingBuilding = GetComponentFast<GoodConsumingBuilding>();
    _goodConsumingToggle = _goodConsumingBuilding.GetGoodConsumingToggle();
    _mechanicalNode = GetComponentFast<MechanicalNode>();
    _pausableBuilding = GetComponentFast<PausableBuilding>();
    _pausableBuilding.PausedChanged += (_, _) => UpdateRegistration();
  }

  protected override void UpdateRegistration() {
    if (Automate && !_pausableBuilding.Paused && !SmartPowerService.IsGeneratorRegistered(this)) {
      SmartPowerService.RegisterGenerator(this);
    }
    if ((!Automate || _pausableBuilding.Paused) && SmartPowerService.IsGeneratorRegistered(this)) {
      if (IsSuspended) {
        Resume();
      }
      SmartPowerService.UnregisterGenerator(this);
    }
  }
}
