// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.SmartPower.Settings;
using Timberborn.DuplicationSystem;
using Timberborn.GoodConsumingBuildingSystem;

namespace IgorZ.SmartPower.PowerGenerators;

sealed class SmartGoodConsumingGenerator : PowerOutputBalancer, IDuplicable<SmartGoodConsumingGenerator> {

  #region PowerOutputBalancer overrides

  /// <inheritdoc/>
  protected override void Suspend() {
    base.Suspend();
    _goodConsumingToggle.PauseConsumption();
    if (MechanicalNode.Graph != null) {
      MechanicalNode.SetOutputMultiplier(0);
    }
  }

  /// <inheritdoc/>
  protected override void Resume() {
    _goodConsumingToggle.ResumeConsumption();
    if (MechanicalNode.Graph != null && _goodConsumingBuilding.HoursUntilNoSupply() > 0) {
      MechanicalNode.SetOutputMultiplier(1.0f);
    }
    base.Resume();
  }

  /// <inheritdoc/>
  protected override bool CanBeAutomated => _goodConsumingBuilding.HasSupplies();

  /// <inheritdoc/>
  protected override void OnAfterSmartLogic() {
    // We can't know if our component ticks before or after GoodConsumingBuilding, so repeat the IsConsuming logic.
    var isConsuming = !_goodConsumingBuilding.ConsumptionPaused
        && _goodConsumingBuilding._blockableObject.IsUnblocked
        && _goodConsumingBuilding.HasSupplies();
    MechanicalNode.SetOutputMultiplier(isConsuming ? 1.0f : 0f);
  }

  /// <inheritdoc/>
  public override void Awake() {
    ShowFloatingIcon = GoodConsumingGeneratorSettings.ShowFloatingIcon;
    Automate = true;
    base.Awake();

    _goodConsumingBuilding = GetComponent<GoodConsumingBuilding>();
    _goodConsumingToggle = _goodConsumingBuilding.GetGoodConsumingToggle();
  }

  #endregion

  #region IDuplicable implementation. Need to be called from descendants when the building is duplicated.

  /// <summary>Copies settings from a source of the same type.</summary>
  public void DuplicateFrom(SmartGoodConsumingGenerator source) {
    base.DuplicateFrom(source);
  }

  #endregion

  #region Implementation

  GoodConsumingBuilding _goodConsumingBuilding;
  GoodConsumingToggle _goodConsumingToggle;

  #endregion
}
