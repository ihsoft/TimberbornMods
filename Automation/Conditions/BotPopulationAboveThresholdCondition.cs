// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;

namespace IgorZ.Automation.Conditions {

/// <summary>Triggers when the current bots population goes below the threshold.</summary>
// ReSharper disable once UnusedType.Global
public sealed class BotPopulationAboveThresholdCondition : BotPopulationThresholdCondition {

  const string DescriptionLocKey = "IgorZ.Automation.BotPopulationAboveThresholdCondition.Description";

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey, GetArgument());

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new BotPopulationAboveThresholdCondition {
        Value = Value,
        RelativeTo = RelativeTo,
        Threshold = Threshold,
    };
  }

  /// <inheritdoc/>
  protected override bool CheckCondition() {
    return DistrictPopulation.NumberOfBots > Threshold;
  }
}

}
