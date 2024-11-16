// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.SmartPower.Settings;
using Timberborn.BuildingsBlocking;

namespace IgorZ.SmartPower.PowerGenerators;

sealed class SmartWalkerPoweredGenerator : PowerOutputBalancer {

  #region PowerOutputBalancer overrides

  /// <inheritdoc/>
  protected override void Suspend() {
    base.Suspend();
    _blockableBuilding.Block(this);
  }

  /// <inheritdoc/>
  protected override void Resume() {
    base.Resume();
    _blockableBuilding.Unblock(this);
  }

  #endregion

  #region Implementation

  WalkerPoweredGeneratorSettings _settings;
  BlockableBuilding _blockableBuilding;

  [Inject]
  public void InjectDependencies(WalkerPoweredGeneratorSettings settings) {
    _settings = settings;
  }

  protected override void Awake() {
    ShowFloatingIcon = _settings.ShowFloatingIcon.Value;
    SuspendDelayedAction = SmartPowerService.GetTimeDelayedAction(_settings.SuspendDelayMinutes.Value);
    ResumeDelayedAction = SmartPowerService.GetTimeDelayedAction(_settings.ResumeDelayMinutes.Value);
    base.Awake();

    _blockableBuilding = GetComponentFast<BlockableBuilding>();
  }

  #endregion
}
