// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Persistence;

namespace Automation.Conditions {

/// <summary>A base condition class for the bots population related conditions.</summary>
/// <remarks>It tracks the population change and does basic values calculations.</remarks>
public abstract class BotPopulationTrackerCondition : PopulationTrackerConditionBase {

  #region API
  // ReSharper disable MemberCanBeProtected.Global

  /// <summary>Positive or negative delta value to use in the relative setting.</summary>
  /// <seealso cref="RelativeToCurrentLevel"/>
  public int Difference { get; protected set; }

  /// <summary>
  /// Indicates that the threshold should be calculated based on teh current population in the district.
  /// </summary>
  /// <seealso cref="Difference"/>
  public bool RelativeToCurrentLevel { get; protected set; }

  /// <summary>Fixed threshold to check the condition against.</summary>
  public int Threshold { get; protected set; } = -1;

  // ReSharper restore MemberCanBeProtected.Global
  #endregion

  #region AutomationConditionBase overrides

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    base.OnBehaviorAssigned();
    if (Threshold != -1) {
      return;  // Was loaded.
    }
    if (DistrictPopulation == null) {
      Threshold = 0;
      return;
    }
    if (RelativeToCurrentLevel) {
      Threshold = Difference + DistrictPopulation.NumberOfBots;
    } else {
      Threshold = Difference;
    }
    if (Threshold < 0) {
      Threshold = 0;
    }
  }

  #endregion

  #region IGameSerializable implemenation

  static readonly PropertyKey<int> DifferenceKey = new("Difference");
  static readonly PropertyKey<bool> RelativeToCurrentLevelKey = new("RelativeToCurrentLevel");
  static readonly PropertyKey<int> ThresholdKey = new("Threshold");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    if (objectLoader.Has(DifferenceKey)) {
      Difference = objectLoader.Get(DifferenceKey);
    }
    RelativeToCurrentLevel = objectLoader.Has(RelativeToCurrentLevelKey) && objectLoader.Get(RelativeToCurrentLevelKey);
    if (objectLoader.Has(ThresholdKey)) {
      Threshold = objectLoader.Get(ThresholdKey);
    }
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(DifferenceKey, Difference);
    objectSaver.Set(RelativeToCurrentLevelKey, RelativeToCurrentLevel);
    if (Threshold != -1) {
      objectSaver.Set(ThresholdKey, Threshold);
    }
  }

  #endregion
}

}
