﻿// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.DwellingSystem;

namespace IgorZ.Automation.Conditions;

/// <summary>A base condition class for the beaver population related conditions.</summary>
/// <remarks>It tracks the population change and does basic value calculations.</remarks>
public abstract class BeaverPopulationThresholdCondition : PopulationThresholdConditionBase {

  #region PopulationThresholdConditionBase overrides

  /// <inheritdoc/>
  protected override void CalculateInitialValues() {
    switch (RelativeTo) {
      case RelativeToEnum.None:
        Threshold = Value;
        break;
      case RelativeToEnum.CurrentLevel:
        Threshold = Value + DistrictPopulation.NumberOfAdults + DistrictPopulation.NumberOfChildren;
        break;
      case RelativeToEnum.MaxLevel:
        var statsProvider = DistrictCenter.GetComponentFast<DistrictDwellingStatisticsProvider>();
        var dwellingStats = statsProvider.GetDwellingStatistics();
        Threshold = Value + dwellingStats.FreeBeds + dwellingStats.OccupiedBeds;
        break;
      default:
        throw new ArgumentOutOfRangeException();
    }
    if (Threshold < 0) {
      Threshold = 0;
    }
  }

  #endregion
}