// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;
using Timberborn.GameDistricts;

namespace Automation.Conditions {

/// <summary>Triggers when the current bots population goes above the threshold.</summary>
// ReSharper disable once UnusedType.Global
public sealed class BotPopulationBelowThresholdCondition : BotPopulationTrackerCondition {
  const string DescriptionLocKey = "IgorZ.Automation.BotPopulationBelowThresholdCondition.Description";

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey, Threshold);

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new BotPopulationBelowThresholdCondition {
        Difference = Difference,
        RelativeToCurrentLevel = RelativeToCurrentLevel,
        Threshold = Threshold,
    };
  }

  /// <inheritdoc/>
  protected override void OnPopulationChanged() {
    ConditionState = DistrictPopulation.NumberOfBots < Threshold;
  }

  /// <inheritdoc/>
  protected override void OnBuildingDistrictCenterChange(DistrictCenter oldCenter) {
  }
}

}
