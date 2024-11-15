// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.SmartPower.Utils;
using Timberborn.BuildingsBlocking;

namespace IgorZ.SmartPower.PowerGenerators;

sealed class SmartWalkerPoweredGenerator : PowerOutputBalancer {

  #region PowerOutputBalancer overrides

  /// <inheritdoc/>
  protected override void GetActionDelays(out TickDelayedAction resumeAction, out TickDelayedAction suspendAction) {
    // FIXME: read from settings.
    resumeAction = SmartPowerService.GetTimeDelayedAction(15);
    suspendAction = SmartPowerService.GetTimeDelayedAction(30);
  }

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

  BlockableBuilding _blockableBuilding;

  protected override void Awake() {
    base.Awake();
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
  }

  #endregion
}
