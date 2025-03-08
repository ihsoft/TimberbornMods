// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Value that can be passed around in the scripting engine.</summary>
public record ScriptValue {
  /// <summary>Type of the value.</summary>
  public enum TypeEnum {
    /// <summary>
    /// An integer that represents a 2-digit fixed precision float. For example, "1.5f" would be represented as "150".
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

  /// <summary>Creates a new value that represents a number.</summary>
  /// <remarks>All numbers are 2-digits fixed precision numbers. Value "1234" should be treated as "12.34f".</remarks>
  public static ScriptValue Of(int number) {
    return new ScriptValue { _number = number };
  }

  /// <summary>Creates a new value that represents a normal float value.</summary>
  /// <remarks>Value "12.34f" will be transformed in to a 2-digit fixed precision number "1234".</remarks>
  public static ScriptValue FromFloat(float value) {
    return new ScriptValue { _number = Mathf.RoundToInt(value * 100f) };
  }

  /// <summary>Current numeric value.</summary>
  /// <remarks>All numbers are 2-digits fixed precision numbers. Value "1234" should be treated as "12.34f".</remarks>
  /// <exception cref="ScriptError">if the value is not numeric.</exception>
  public int AsNumber {
    get {
      if (!_number.HasValue) {
        throw new ScriptError("Value is not a number: " + ToString());
      }
      return _number.Value;
    }
  }

  /// <summary>Current string value.</summary>
  /// <exception cref="ScriptError">if the value is a string.</exception>
  public string AsString {
    get {
      if (_string == null) {
        throw new ScriptError("Value is not a string: " + ToString());
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

  int? _number;
  string _string;
}
