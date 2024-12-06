// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.Values;

/// <summary>Expression value that represents a constant string.</summary>
sealed class StringValue : IExpressionValue {

  readonly string _value;

  StringValue(string value) {
    _value = value;
  }

  #region Factories

  /// <summary>Creates string value from literal.</summary>
  public static StringValue FromLiteral(string literal) {
    return new StringValue(literal);
  }

  #endregion

  #region IExpressionValue implementation

  /// <inheritdoc/>
  public IExpressionValue.ValueTypeEnum ValueType => IExpressionValue.ValueTypeEnum.String;

  /// <inheritdoc/>
  public int Compare(IExpressionValue other) {
    return String.Compare(_value, other.AsString(), StringComparison.Ordinal);
  }

  /// <inheritdoc/>
  public string AsString() => _value;

  /// <inheritdoc/>
  public int AsNumber() {
    try {
      return Mathf.RoundToInt(float.Parse(_value) * 100);
    } catch (FormatException) {
      throw new ScriptError("Cannot convert string to int: " + _value);
    }
  }

  /// <inheritdoc/>
  public bool AsBool() {
    var lower = _value.ToLower();
    switch (lower) {
      case "true":
        return true;
      case "false":
        return false;
      default:
        try {
          return Mathf.RoundToInt(float.Parse(_value) * 100) != 0;
        } catch (FormatException) {
          throw new ScriptError("Cannot convert string to bool: " + _value);
        }
    }
  }

  #endregion

  #region Object overrides

  /// <inheritdoc/>
  public override string ToString() => $"{ValueType}#{_value}";

  #endregion
}