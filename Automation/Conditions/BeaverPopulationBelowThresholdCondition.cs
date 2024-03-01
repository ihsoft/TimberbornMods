// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;
using Timberborn.GameDistricts;

namespace Automation.Conditions {

/// <summary>Triggers when the current beavers population goes above the threshold.</summary>
// ReSharper disable once UnusedType.Global
public sealed class BeaverPopulationBelowThresholdCondition : BeaverPopulationTrackerCondition {
  const string DescriptionLocKey = "IgorZ.Automation.BeaverPopulationBelowThresholdCondition.Description";

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey, Threshold);

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new BeaverPopulationBelowThresholdCondition {
        Difference = Difference,
        RelativeToCurrentLevel = RelativeToCurrentLevel,
        RelativeToMaxLevel = RelativeToMaxLevel,
        Threshold = Threshold
    };
  }

  /// <inheritdoc/>
  public override void SyncState() {
    OnPopulationChanged();
  }

  /// <inheritdoc/>
  protected override void OnPopulationChanged() {
    var currentPopulation = DistrictPopulation.NumberOfAdults + DistrictPopulation.NumberOfChildren;
    ConditionState = currentPopulation < Threshold;
  }

  /// <inheritdoc/>
  protected override void OnBuildingDistrictCenterChange(DistrictCenter oldCenter) {
  }
}

}
