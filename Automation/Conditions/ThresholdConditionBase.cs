// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.Persistence;

namespace IgorZ.Automation.Conditions {

/// <summary>A base class for the conditions that checks a single value against the constant.</summary>
/// <remarks>
/// The descendants must define the threshold to check against via <see cref="CalculateThreshold"/>. Then, when the
/// value has changed, the condition can be checked via <see cref="CheckValue"/>.
/// </remarks>
public abstract class ThresholdConditionBase : AutomationConditionBase {

  #region API
  // ReSharper disable MemberCanBeProtected.Global
  // ReSharper disable MemberCanBePrivate.Global

  /// <summary>The type of the check to perform.</summary>
  public enum CheckTypeEnum {
    /// <summary>
    /// The check type is not set. It is an invalid state and must be set before the condition can be used.
    /// </summary>
    Unset,
    /// <summary>The condition is met if the value is below the threshold.</summary>
    Below,
    /// <summary>The condition is met if the value is below or equal to the threshold.</summary>
    BelowOrEqual,
    /// <summary>The condition is met if the value is equal to the threshold.</summary>
    Equal,
    /// <summary>The condition is met if the value is above or equal to the threshold.</summary>
    AboveOrEqual,
    /// <summary>The condition is met if the value is above the threshold.</summary>
    Above,
  }

  /// <summary>The type of the check to perform.</summary>
  public CheckTypeEnum CheckType { get; protected set; } = CheckTypeEnum.Unset;

  /// <summary>Tells whether the condition threshold is initialized and ready to be checked.</summary>
  /// <seealso cref="Threshold"/>
  protected bool IsInitialized { get; private set; }

  /// <summary>The calculated value to check the condition against. It must not be set in the template.</summary>
  /// <remarks>
  /// This value must be calculated first. If used before this moment, then the behavior is undetermined.
  /// </remarks>
  /// <seealso cref="CalculateThreshold"/>
  public int Threshold { get; protected set; }

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore MemberCanBeProtected.Global

  /// <summary>A callback that is called before resetting the current threshold.</summary>
  /// <remarks>Descendants should reset their internal state if any.</remarks>
  /// <seealso cref="CalculateThreshold"/>
  protected virtual void OnBeforeResetThreshold() {}

  /// <summary>Calculates <see cref="Threshold"/> based on the current game state.</summary>
  /// <seealso cref="OnBeforeResetThreshold"/>
  protected abstract int CalculateThreshold();

  /// <summary>Gets the string representation of the current <see cref="CheckType"/> for the UI purpose.</summary>
  /// <remarks>The returned value must be localized.</remarks>
  /// <exception cref="InvalidOperationException"></exception>
  /// FIXME: Localize the strings.
  protected string GetCheckTypeString() {
    return CheckType switch {
        CheckTypeEnum.Below => "below",
        CheckTypeEnum.BelowOrEqual => "below or equals to",
        CheckTypeEnum.Equal => "equals to",
        CheckTypeEnum.Above => "above",
        CheckTypeEnum.AboveOrEqual => "above or equals to",
        CheckTypeEnum.Unset => throw new InvalidOperationException("Unset check type!"),
        _ => throw new InvalidOperationException($"Unknown check type: {CheckType}")
    };
  }

  /// <summary>Verifies the provided value against the current <see cref="Threshold"/>.</summary>
  /// <remarks>Must not be called if the state is not initialized.</remarks>
  /// <seealso cref="IsInitialized"/>
  protected virtual void CheckValue(int value) {
    if (!IsInitialized) {
      throw new InvalidOperationException("The state is not initialized yet.");
    }
    ConditionState = CheckType switch {
        CheckTypeEnum.Below => value < Threshold,
        CheckTypeEnum.BelowOrEqual => value <= Threshold,
        CheckTypeEnum.Equal => value == Threshold,
        CheckTypeEnum.Above => value > Threshold,
        CheckTypeEnum.AboveOrEqual => value >= Threshold,
        CheckTypeEnum.Unset => throw new InvalidOperationException("Unset check type!"),
        _ => throw new InvalidOperationException($"Unknown check type: {CheckType}")
    };
  }

  #endregion

  #region AutomationConditionBase overrides

  /// <inheritdoc/>
  public override void SyncState() {
    if (!IsInitialized) {
      IsInitialized = true;
      Threshold = CalculateThreshold();
    }
  }

  #endregion

  #region IGameSerializable implemenation

  static readonly PropertyKey<string> CheckTypeToKey = new("CheckType");
  static readonly PropertyKey<int> ThresholdKey = new("Threshold");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    CheckType = (CheckTypeEnum)Enum.Parse(typeof(CheckTypeEnum), objectLoader.Get(CheckTypeToKey), ignoreCase: false);
    if (objectLoader.Has(ThresholdKey)) {
      Threshold = objectLoader.Get(ThresholdKey);
      IsInitialized = true;
    }
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(CheckTypeToKey, CheckType.ToString());
    if (IsInitialized) {
      objectSaver.Set(ThresholdKey, Threshold);
    }
  }

  #endregion

}

}
