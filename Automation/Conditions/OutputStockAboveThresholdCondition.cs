// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;

namespace Automation.Conditions {

public sealed class OutputStockAboveThresholdCondition : OutputStockThresholdConditionBase {
  /// <inheritdoc/>
  public override string UiDescription =>
      string.Format("<SolidHighlight>output stock above {0}%</SolidHighlight>", Threshold);

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
