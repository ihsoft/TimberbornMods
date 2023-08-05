// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;

namespace Automation.Conditions {

public sealed class OutputStockBelowThresholdCondition : OutputStockThresholdConditionBase {
  const string DescriptionLocKey = "IgorZ.Automation.OutputStockBelowThresholdCondition.Description";

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey, Threshold);

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new OutputStockBelowThresholdCondition { Threshold = Threshold };
  }

  /// <inheritdoc/>
  protected override void CheckInventory() {
    var storageFillRatio = Inventory.TotalAmountInStock * 100 / Inventory.Capacity;
    ConditionState = storageFillRatio < Threshold;
  }
}

}
