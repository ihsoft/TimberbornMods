// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

sealed class ScriptValue {
  public enum ValueType {
    Number,
    String,
  }
  public ValueType Type => _number.HasValue ? ValueType.Number : ValueType.String;

  public static ScriptValue Of(string literal) {
    return new ScriptValue { _string = literal };
  }

  public static ScriptValue Of(int number) {
    return new ScriptValue { _number = number };
  }

  /// <summary>Current numeric value.</summary>
  /// <remarks>All numbers are 2-digits fixed point numbers. Value "1234" should be treated as "12.34f".</remarks>
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

  public override string ToString() {
    return _number.HasValue ? _number.Value.ToString() : _string;
  }

  int? _number;
  string _string;
}
