// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;

namespace IgorZ.Automation.Conditions;

/// <summary>Triggers when the current bots population goes above the threshold.</summary>
// ReSharper disable once UnusedType.Global
public sealed class BotPopulationBelowThresholdCondition : BotPopulationThresholdCondition {

  const string DescriptionLocKey = "IgorZ.Automation.BotPopulationBelowThresholdCondition.Description";

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey, GetArgument());

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new BotPopulationBelowThresholdCondition {
        Value = Value,
        RelativeTo = RelativeTo,
        Threshold = Threshold,
    };
  }

  /// <inheritdoc/>
  protected override bool CheckCondition() {
    return DistrictPopulation.NumberOfBots < Threshold;
  }

}