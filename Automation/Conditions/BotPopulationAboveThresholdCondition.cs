// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;
using Timberborn.GameDistricts;

namespace Automation.Conditions {

/// <summary>Triggers when the current bots population goes below the threshold.</summary>
// ReSharper disable once UnusedType.Global
public sealed class BotPopulationAboveThresholdCondition : BotPopulationTrackerCondition {
  const string DescriptionLocKey = "IgorZ.Automation.BotPopulationAboveThresholdCondition.Description";

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey, Threshold);

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new BotPopulationAboveThresholdCondition {
        Difference = Difference,
        RelativeToCurrentLevel = RelativeToCurrentLevel,
        Threshold = Threshold,
    };
  }

  /// <inheritdoc/>
  public override void SyncState() {
    OnPopulationChanged();
  }

  /// <inheritdoc/>
  protected override void OnPopulationChanged() {
    ConditionState = DistrictPopulation.NumberOfBots > Threshold;
  }

  /// <inheritdoc/>
  protected override void OnBuildingDistrictCenterChange(DistrictCenter oldCenter) {
  }
}

}
