// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.TimberDev.UI;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

/// <summary>Value that can be passed around in the scripting engine.</summary>
/// <remarks>
/// <p>
/// In general, it works with only two type: string and numeric. The string one is self-explanatory. The numeric one is
/// tricky. It's stored as a 2-digit fixed precision real number. That is, under the hood, it's an INTEGER, but the last
/// two digits if that integer is reserved for the fractional part of the float. For example, "1.5f" would be stored as
/// "150".
/// </p>
/// <p>
/// There is virtually no way to store a number with more than 2 digits after the comma, and if you try to do that, the
/// value will be silently rounded. Keep it in mind when you are doing calculations with the values, because the
/// precision loss can happen without any warning.
/// </p>
/// <p>
/// If you need to increase precision, multiply the dividend by 100 before doing the calculations and use the multiplied
/// result values (don't divide them back!). As long as the number stays in the 32-bit signed integer range, the
/// precision will be preserved. If you go over it, the beavers god may punish you somehow (nobody knows how exactly).
/// </p>
/// </remarks>
/// <example>
/// <list>
/// <item>"10/3*3" will be evaluated as "9.99" instead of "10".</item>
/// <item>"100*3/3" will be evaluated as "10" as expected.</item>
/// <item>"1.005" will be stored as "100", which is "1.0f".</item>
/// <item>"1.999" will be stored as "200", which is "2.0f".</item>
/// <item>"1.005 / 2" will be evaluated as "50", which is "0.5f".</item>
/// <item>"10.05 / 20 / 10" will be evaluated as "50", which is "0.5f".</item>
/// </list>
/// </example>
record struct ScriptValue : IComparable<ScriptValue> {
  /// <summary>
  /// A special value that represents an invalid value. It can be used as a default value for uninitialized variables.
  /// </summary>
  public static ScriptValue InvalidValue = new();

  /// <summary>Type of the value.</summary>
  public enum TypeEnum {
    /// <summary>No type set or detected. The upstream code decides how to handle it.</summary>
    Unset,
    /// <summary>
    /// An integer that represents a 2-digit fixed precision real number.
    /// For example, "1.5f" would be represented as "150".
    /// </summary>
    Number,

    /// <summary>A string value.</summary>
    String,
  }

  /// <summary>Value type.</summary>
  public TypeEnum ValueType {
    get {
      if (_number.HasValue) {
        return TypeEnum.Number;
      }
      if (_string != null) {
        return TypeEnum.String;
      }
      return TypeEnum.Unset;
    }
  }

  /// <summary>Creates a new value that represents a string literal.</summary>
  public static ScriptValue FromString(string literal) {
    return new ScriptValue { _string = literal };
  }

  /// <summary>Creates a new value from a raw numeric value.</summary>
  /// <param name="number">A 2-digits fixed precision real number.</param>
  public static ScriptValue Of(int number) {
    return new ScriptValue { _number = number };
  }

  /// <summary>Creates a new value that represents a float value.</summary>
  /// <remarks>This method will silently round the value to the last 2 digits after the comma.</remarks>
  public static ScriptValue FromFloat(float value) {
    return new ScriptValue { _number = Mathf.RoundToInt(value * 100f) };
  }

  /// <summary>Creates a new value that represents an integer value.</summary>
  public static ScriptValue FromInt(int value) {
    return new ScriptValue { _number = value * 100 };
  }

  /// <summary>Creates a new value that represents a boolean value.</summary>
  public static ScriptValue FromBool(bool flag) {
    return new ScriptValue { _number = flag ? 100 : 0 };
  }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
  public static ScriptValue operator -(ScriptValue value) {
    return new ScriptValue { _number = -value.AsNumber };
  }

  public static ScriptValue operator +(ScriptValue left, ScriptValue right) {
    return new ScriptValue { _number = left.AsNumber + right.AsNumber };
  }

  public static ScriptValue operator -(ScriptValue left, ScriptValue right) {
    return new ScriptValue { _number = left.AsNumber - right.AsNumber };
  }

  public static ScriptValue operator *(ScriptValue left, ScriptValue right) {
    return new ScriptValue { _number = Mathf.RoundToInt(left.AsNumber * right.AsNumber / 100f) };
  }

  /// <exception cref="ScriptError">if dividing by zero.</exception>
  public static ScriptValue operator /(ScriptValue left, ScriptValue right) {
    if (right.AsNumber == 0) {
      throw new ScriptError.DivisionByZero();
    }
    return new ScriptValue { _number = Mathf.RoundToInt(left.AsNumber * 100f / right.AsNumber) };
  }

  public int CompareTo(ScriptValue other) {
    if (ValueType != other.ValueType) {
      throw new InvalidOperationException($"Cannot compare values of different types: {this} vs {other}");
    }
    return ValueType switch {
        TypeEnum.Number => AsNumber.CompareTo(other.AsNumber),
        TypeEnum.String => string.Compare(AsString, other.AsString, StringComparison.Ordinal),
        _ => throw new InvalidOperationException("Unknown ScriptValue type: " + ValueType),
    };
  }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

  /// <summary>The numeric value is a 2-digits fixed precision real number multiplied by x100.</summary>
  /// <remarks>
  /// For example, value "1234" means "12.34f". The maximum possible precision is two digits after the point.
  /// </remarks>
  /// <exception cref="ScriptError">if the value is not a number.</exception>
  public int AsNumber {
    get {
      if (!_number.HasValue) {
        throw new ScriptError.BadValue("Value is not a number: " + ToString());
      }
      return _number.Value;
    }
  }

  /// <summary>Current numeric value as a float.</summary>
  /// <exception cref="ScriptError">if the value is not a number.</exception>
  public float AsFloat => AsNumber / 100f;

  /// <summary>Current numeric value as an integer.</summary>
  /// <exception cref="ScriptError">if the value is not a number.</exception>
  public int AsInt => Mathf.RoundToInt(AsNumber / 100f);

  /// <summary>Current string value.</summary>
  /// <exception cref="ScriptError">if the value is not a string.</exception>
  public string AsString {
    get {
      if (_string == null) {
        throw new ScriptError.BadValue($"Value is not a string: {ToString()}");
      }
      return _string;
    }
  }

  /// <inheritdoc/>
  public override string ToString() {
    return ValueType switch {
        TypeEnum.Number => $"ScriptValue#Number:{AsNumber.ToString()}",
        TypeEnum.String => $"ScriptValue#String:{AsString}",
        _ => $"ScriptValue#{ValueType}:UNKNOWN",
    };
  }

  /// <summary>Formats the value according to the value definition.</summary>
  /// <param name="valueDef">
  /// Optional value definition. If not provided, then the string types are presented "as-is", and the number types are
  /// converted to floats and formatted as "0.##".
  /// </param>
  public string FormatValue(ValueDef valueDef) {
    var stringValue = ValueType switch {
        TypeEnum.Number => valueDef.DisplayNumericFormat switch {
            ValueDef.NumericFormatEnum.Float => AsFloat.ToString("0.00"),
            ValueDef.NumericFormatEnum.Percent => AsFloat.ToString("0%"),
            ValueDef.NumericFormatEnum.Integer => AsInt.ToString(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(valueDef.DisplayNumericFormat), valueDef.DisplayNumericFormat, null),
        },
        TypeEnum.String => AsString,
        TypeEnum.Unset => throw new InvalidOperationException($"Cannot format value: {this}"),
        _ => throw new ArgumentOutOfRangeException(nameof(ValueType), ValueType, null),
    };
    if (valueDef?.Options == null) {
      return stringValue;
    }
    var resolvedValue = valueDef.Options.FirstOrDefault(x => x.Value == stringValue);
    return resolvedValue.Text ?? CommonFormats.HighlightRed("?" + stringValue);
  }

  int? _number;
  string _string;
}
