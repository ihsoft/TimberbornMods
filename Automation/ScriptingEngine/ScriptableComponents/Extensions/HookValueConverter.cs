// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Converts values at the boundary between configured CLR members and the scripting engine.</summary>
static class HookValueConverter {
  /// <summary>Converts a getter result, preserving double precision until fixed-point rounding.</summary>
  public static ScriptValue ToScript(Type type, object value) {
    if (value == null || value.GetType() != type) {
      throw new ScriptError.BadValue("Unexpected configured value type.");
    }
    if (type == typeof(string)) {
      return ScriptValue.FromString((string)value);
    }
    if (type == typeof(bool)) {
      return ScriptValue.FromBool((bool)value);
    }
    var number = Convert.ToDouble(value);
    var scaled = Math.Round(number * 100, MidpointRounding.ToEven);
    if (double.IsNaN(number) || double.IsInfinity(number) || scaled < int.MinValue || scaled > int.MaxValue) {
      throw new ScriptError.ValueOutOfRange("Configured value exceeds the scripting numeric range.");
    }
    return ScriptValue.Of((int)scaled);
  }

  /// <summary>Converts a method argument to its declared CLR type; fractional integers are rejected.</summary>
  public static object FromScript(Type type, ScriptValue value) {
    if (type == typeof(string)) {
      return value.AsString;
    }
    if (type == typeof(bool)) {
      return value.AsRawNumber != 0;
    }
    if (type == typeof(int)) {
      if (value.AsRawNumber % 100 != 0) {
        throw new ScriptError.BadValue("An integer argument is required.");
      }
      return value.AsRawNumber / 100;
    }
    if (type == typeof(float)) {
      return value.AsFloat;
    }
    if (type == typeof(double)) {
      return value.AsRawNumber / 100d;
    }
    throw new ScriptError.BadValue("Unsupported configured value type: " + type);
  }
}
