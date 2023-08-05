// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;

namespace Automation.Conditions {

public sealed class OutputStockAboveThresholdCondition : OutputStockThresholdConditionBase {
  const string DescriptionLocKey = "IgorZ.Automation.OutputStockAboveThresholdCondition.Description";

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey, Threshold);

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new OutputStockAboveThresholdCondition { Threshold = Threshold };
  }

  /// <inheritdoc/>
  protected override void CheckInventory() {
    var storageFillRatio = Inventory.TotalAmountInStock * 100 / Inventory.Capacity;
    ConditionState = storageFillRatio > Threshold;
  }
}

}
