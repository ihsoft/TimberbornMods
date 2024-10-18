// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;

namespace IgorZ.Automation.Conditions {

/// <summary>Triggers when the current beavers population goes above the threshold.</summary>
// ReSharper disable once UnusedType.Global
public sealed class BeaverPopulationAboveThresholdCondition : BeaverPopulationThresholdCondition {

  const string DescriptionLocKey = "IgorZ.Automation.BeaverPopulationAboveThresholdCondition.Description";

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey, GetArgument());

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new BeaverPopulationAboveThresholdCondition {
        Value = Value,
        RelativeTo = RelativeTo,
        Threshold = Threshold
    };
  }

  /// <inheritdoc/>
  protected override bool CheckCondition() {
    var currentPopulation = DistrictPopulation.NumberOfAdults + DistrictPopulation.NumberOfChildren;
    return currentPopulation > Threshold;
  }
}

}
