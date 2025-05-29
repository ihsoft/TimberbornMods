// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.TimberDev.UI;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Value that can be passed around in the scripting engine.</summary>
record struct ScriptValue : IComparable<ScriptValue> {
  /// <summary>Type of the value.</summary>
  public enum TypeEnum {
    /// <summary>
    /// An integer that represents a 2-digit fixed precision real number.
    /// For example, "1.5f" would be represented as "150".
    /// </summary>
    Number,

    /// <summary>A string value.</summary>
    String,
  }

  /// <summary>Value type.</summary>
  public TypeEnum ValueType => _number.HasValue ? TypeEnum.Number : TypeEnum.String;

  /// <summary>Creates a new value that represents a string literal.</summary>
  public static ScriptValue Of(string literal) {
    return new ScriptValue { _string = literal };
  }

  /// <summary>Creates a new value from a raw number.</summary>
  /// <param name="number">A 2-digits fixed precision real number.</param>
  public static ScriptValue Of(int number) {
    return new ScriptValue { _number = number };
  }

  /// <summary>Creates a new value that represents a float value.</summary>
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
  public static ScriptValue operator +(ScriptValue left, ScriptValue right) {
    return new ScriptValue { _number = left.AsNumber + right.AsNumber };
  }

  public static ScriptValue operator -(ScriptValue left, ScriptValue right) {
    return new ScriptValue { _number = left.AsNumber - right.AsNumber };
  }

  public static ScriptValue operator *(ScriptValue left, ScriptValue right) {
    return new ScriptValue { _number = left.AsNumber * right.AsNumber / 100 };
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

  /// <exception cref="ScriptError">if dividing by zero.</exception>
  public static ScriptValue operator /(ScriptValue left, ScriptValue right) {
    if (right.AsNumber == 0) {
      throw new ScriptError.RuntimeError("Division by zero");
    }
    return new ScriptValue { _number = left.AsNumber * 100 / right.AsNumber};
  }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

  /// <summary>The numeric value is a 2-digits fixed precision real number multiplied by x100.</summary>
  /// <remarks>
  /// For example, value "1234" means "12.34f". The maximum possible precision is two digits after the point.
  /// </remarks>
  /// <exception cref="ScriptError">if the value is not numeric.</exception>
  public int AsNumber {
    get {
      if (!_number.HasValue) {
        throw new ScriptError.RuntimeError("Value is not a number: " + ToString());
      }
      return _number.Value;
    }
  }

  /// <summary>Current numeric value as a float.</summary>
  public float AsFloat => AsNumber / 100f;

  /// <summary>Current numeric value as an integer.</summary>
  public int AsInt => Mathf.RoundToInt(AsNumber / 100f);

  /// <summary>Current string value.</summary>
  /// <exception cref="ScriptError">if the value is a string.</exception>
  public string AsString {
    get {
      if (_string == null) {
        throw new ScriptError.RuntimeError("Value is not a string: " + ToString());
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
    if (valueDef == null) {
      return ValueType == TypeEnum.Number ? AsFloat.ToString("0.##") : AsString;
    }
    if (valueDef.ValueFormatter != null) {
      return valueDef.ValueFormatter(this);
    }
    if (valueDef.ValueType != TypeEnum.String) {
      return AsFloat.ToString("0.##");
    }
    var stringValue = AsString;
    if (valueDef.Options == null) {
      return stringValue;
    }
    var resolvedValue = valueDef.Options.FirstOrDefault(x => x.Value == stringValue);
    return resolvedValue.Text != "" ? resolvedValue.Text : CommonFormats.HighlightRed("?" + stringValue);
  }

  int? _number;
  string _string;
}
