// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.SmartPower.Settings;
using Timberborn.BlockSystem;
using Timberborn.GoodConsumingBuildingSystem;

namespace IgorZ.SmartPower.PowerGenerators;

sealed class SmartGoodConsumingGenerator : PowerOutputBalancer, IUnfinishedStateListener {

  #region IUnfinishedStateListener implementation

  public void OnEnterUnfinishedState() {
    Automate = true;  // Enable for all new generators.
  }
  public void OnExitUnfinishedState() {
  }

  #endregion

  #region PowerOutputBalancer overrides

  /// <inheritdoc/>
  protected override void Suspend() {
    base.Suspend();
    _goodConsumingToggle.PauseConsumption();
    if (MechanicalNode.Graph != null) {
      MechanicalNode.UpdateOutput(0);
    }
  }

  /// <inheritdoc/>
  protected override void Resume() {
    _goodConsumingToggle.ResumeConsumption();
    if (MechanicalNode.Graph != null && _goodConsumingBuilding.HoursUntilNoSupply > 0) {
      MechanicalNode.Active = true;
      MechanicalNode.UpdateOutput(1.0f);
    }
    base.Resume();
  }

  protected override void Awake() {
    ShowFloatingIcon = _settings.ShowFloatingIcon.Value;
    base.Awake();

    _goodConsumingBuilding = GetComponentFast<GoodConsumingBuilding>();
    _goodConsumingToggle = _goodConsumingBuilding.GetGoodConsumingToggle();
  }

  #endregion

  #region Implementation

  GoodConsumingGeneratorSettings _settings;
  GoodConsumingBuilding _goodConsumingBuilding;
  GoodConsumingToggle _goodConsumingToggle;

  [Inject]
  public void InjectDependencies(GoodConsumingGeneratorSettings settings) {
    _settings = settings;
  }

  #endregion
}
