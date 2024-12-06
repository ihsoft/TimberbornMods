// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Value that represents a number.</summary>
/// <remarks>
/// Numbers are fixed point float values with a precision of two decimals after the point. Internally, numbers are
/// stored as ints, so it is safe to check them for equality.
/// </remarks>
sealed class NumberValue : IExpressionValue {

  readonly int _value;

  NumberValue(int value) {
    _value = value;
  }

  #region Factories

  /// <summary>Creates number value from int.</summary>
  public static NumberValue FromInt(int value) {
    return new NumberValue(value * 100);
  }

  /// <summary>Creates number value from float.</summary>
  public static NumberValue FromFloat(float value) {
    return new NumberValue(Mathf.RoundToInt(value * 100));
  }

  /// <summary>Creates number value from raw value.</summary>
  public static NumberValue FromRawValue(int value) {
    return new NumberValue(value);
  }

  #endregion

  #region IExpressionValue implementation 

  /// <inheritdoc/>
  public IExpressionValue.ValueTypeEnum ValueType => IExpressionValue.ValueTypeEnum.Number;

  /// <inheritdoc/>
  public int Compare(IExpressionValue other) {
    return _value.CompareTo(other.AsNumber());
  }

  /// <inheritdoc/>
  public string AsString() => (_value / 100f).ToString("0.##");

  /// <inheritdoc/>
  public int AsNumber()  => _value;

  /// <inheritdoc/>
  public bool AsBool() {
    return _value != 0;
  }

  #endregion

  #region Object overrides

  /// <inheritdoc/>
  public override string ToString() => $"{ValueType}#{AsString()}";

  #endregion
}