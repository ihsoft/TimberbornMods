// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace Automation.Conditions {

/// <summary>A base condition class for the bots population related conditions.</summary>
/// <remarks>It tracks the population change and does basic values calculations.</remarks>
public abstract class BotPopulationThresholdCondition : PopulationThresholdConditionBase {

  #region PopulationThresholdConditionBase overrides

  /// <inheritdoc/>
  protected override void UpdateValues() {
    switch (RelativeTo) {
      case RelativeToEnum.None:
        Threshold = Value;
        break;
      case RelativeToEnum.CurrentLevel:
        Threshold = Value + DistrictPopulation.NumberOfBots;
        break;
      case RelativeToEnum.MaxLevel:
        throw new ArgumentOutOfRangeException("Unsupported: " + RelativeTo);
      default:
        throw new ArgumentOutOfRangeException();
    }
    if (Threshold < 0) {
      Threshold = 0;
    }
  }

  #endregion

}

}
