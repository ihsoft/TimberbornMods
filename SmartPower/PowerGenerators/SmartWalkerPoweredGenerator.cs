// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.SmartPower.Settings;
using Timberborn.BlockingSystem;
using Timberborn.DuplicationSystem;

namespace IgorZ.SmartPower.PowerGenerators;

sealed class SmartWalkerPoweredGenerator : PowerOutputBalancer, IDuplicable<SmartWalkerPoweredGenerator> {

  #region PowerOutputBalancer overrides

  /// <inheritdoc/>
  protected override bool CanBeAutomated => true;

  /// <inheritdoc/>
  protected override void Suspend() {
    base.Suspend();
    _blockableObject.Block(this);
  }

  /// <inheritdoc/>
  protected override void Resume() {
    base.Resume();
    _blockableObject.Unblock(this);
  }

  #endregion

  #region IDuplicable implementation. Need to be called from descendants when the building is duplicated.

  /// <summary>Copies settings from a source of the same type.</summary>
  public void DuplicateFrom(SmartWalkerPoweredGenerator source) {
    base.DuplicateFrom(source);
  }

  #endregion

  #region Implementation

  BlockableObject _blockableObject;

  public override void Awake() {
    ShowFloatingIcon = WalkerPoweredGeneratorSettings.ShowFloatingIcon;
    SuspendDelayedAction = SmartPowerService.GetTimeDelayedAction(WalkerPoweredGeneratorSettings.SuspendDelayMinutes);
    ResumeDelayedAction = SmartPowerService.GetTimeDelayedAction(WalkerPoweredGeneratorSettings.ResumeDelayMinutes);
    base.Awake();

    _blockableObject = GetComponent<BlockableObject>();
  }

  #endregion
}
