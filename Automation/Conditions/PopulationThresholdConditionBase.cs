// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.GameDistricts;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;

namespace Automation.Conditions {

/// <summary>A base condition class that check population for a threshold.</summary>
/// <remarks>
/// In descendants, implement <see cref="CalculateInitialValues"/> to calculate <see cref="Threshold"/> and
/// <see cref="CheckCondition"/> to handle the check.
/// </remarks>
public abstract class PopulationThresholdConditionBase : PopulationTrackerConditionBase {

  const string RelativeToMaxArg = "IgorZ.Automation.PopulationRelativeToMaxArg";
  const string RelativeToCurrentArg = "IgorZ.Automation.PopulationRelativeToCurrentArg";

  #region API
  // ReSharper disable MemberCanBeProtected.Global

  /// <summary>Positive or negative value to use to calculate the <see cref="Threshold"/>.</summary>
  /// <seealso cref="RelativeTo"/>
  public int Value { get; protected set; }

  /// <summary>Relative values set.</summary>
  public enum RelativeToEnum {
    /// <summary>Use <see cref="Value"/> as <see cref="Threshold"/>.</summary>
    None, 
    /// <summary>Calculate <see cref="Threshold"/> based on the maximum acceptable quantity.</summary>
    MaxLevel, 
    /// <summary>Calculate <see cref="Threshold"/> based on the current  quantity.</summary>
    CurrentLevel,
  }

  /// <summary>Indicates the relative value to calculate the <see cref="Threshold"/> against.</summary>
  /// <seealso cref="Value"/>
  public RelativeToEnum RelativeTo { get; protected set; }

  /// <summary>The calculated threshold to check the condition against. It must not be set in the template.</summary>
  /// <seealso cref="RelativeTo"/>
  /// <seealso cref="Value"/>
  public int Threshold { get; protected set; } = -1;

  // ReSharper restore MemberCanBeProtected.Global

  /// <summary>Calculates <see cref="Threshold"/> based on the current district and the condition parameters.</summary>
  /// <remarks>The district must be set, or else it's NOOP.</remarks>
  protected abstract void CalculateInitialValues();

  /// <summary>Returns a result of condition checking.</summary>
  protected abstract bool CheckCondition();

  /// <summary>Returns a properly formatted value that wil lbe used to check condition against.</summary>
  /// <remarks>
  /// It can be a relative value in case of <see cref="PopulationTrackerConditionBase.DistrictCenter"/> is not set.
  /// </remarks>
  protected string GetArgument() {
    if (DistrictCenter) {
      return Threshold.ToString();
    }
    return RelativeTo switch {
        RelativeToEnum.None => Value.ToString(),
        RelativeToEnum.CurrentLevel => Behavior.Loc.T(RelativeToCurrentArg, Value != 0 ? Value.ToString("+#;-#") : ""),
        RelativeToEnum.MaxLevel => Behavior.Loc.T(RelativeToMaxArg, Value != 0 ? Value.ToString("+#;-#") : ""),
        _ => throw new ArgumentOutOfRangeException()  // Not expected.
    };
  }

  #endregion

  #region AutomationConditionBase overrides

  /// <inheritdoc/>
  public override void SyncState() {
    if (DistrictCenter) {
      CalculateInitialValues();
    }
    base.SyncState();
  }

  #endregion

  #region PopulationTrackerConditionBase implementation

  /// <inheritdoc/>
  protected override void OnBuildingDistrictCenterChange(DistrictCenter oldCenter) {
    if (oldCenter && !DistrictCenter) {
      // Detached from district.
      Threshold = -1;
    }
    if (DistrictCenter && Threshold == -1) {
      // Attached to district and haven't yet calculated values.
      CalculateInitialValues();
    }
  }

  /// <inheritdoc/>
  protected override void OnPopulationChanged() {
    ConditionState = CheckCondition();
  }

  #endregion

  #region IGameSerializable implemenation

  static readonly PropertyKey<int> ValueKey = new("Value");
  static readonly PropertyKey<string> RelativeToKey = new("RelativeTo");
  static readonly PropertyKey<int> ThresholdKey = new("Threshold");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    // FIXME(ihsoft): Migration v0.14=>v1.0. Drop in the future versions.
    if (!objectLoader.Has(ValueKey)) {
      HostedDebugLog.Warning(Behavior, "Cannot load legacy population condition. Re-apply it!");
      IsMarkedForCleanup = true;
      return;
    }
    Value = objectLoader.Get(ValueKey);
    RelativeTo = (RelativeToEnum)Enum.Parse(typeof(RelativeToEnum), objectLoader.Get(RelativeToKey), ignoreCase: false);
    if (objectLoader.Has(ThresholdKey)) {
      Threshold = objectLoader.Get(ThresholdKey);
    }
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ValueKey, Value);
    objectSaver.Set(RelativeToKey, RelativeTo.ToString());
    if (Threshold != -1) {
      objectSaver.Set(ThresholdKey, Threshold);
    }
  }

  #endregion
}

}
