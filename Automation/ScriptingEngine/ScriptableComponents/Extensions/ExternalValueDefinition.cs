// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using Timberborn.Localization;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Adapts provider types and editor metadata to Automation's fixed-point/string value model.</summary>
sealed class ExternalValueDefinition {
  const string StateOnLocKey = "Automation.State.On";
  const string StateOffLocKey = "Automation.State.Off";

  /// <summary>Native value definition; numeric bounds apply only to editor input.</summary>
  public readonly ValueDef Definition;

  /// <summary>Builds the native editor format and runtime type/option validation for a provider value.</summary>
  public ExternalValueDefinition(Type type, ILoc loc, string[] optionValues, string[] optionNames,
      double? min = null, double? max = null, bool percent = false) {
    _type = type;
    _options = optionValues;
    if (type != typeof(string) && type != typeof(bool) && type != typeof(int)
        && type != typeof(float) && type != typeof(double)) {
      throw new ArgumentException("Unsupported external value type: " + type);
    }
    if (percent && type != typeof(float) && type != typeof(double)) {
      throw new ArgumentException("Percentages require a float or double value.");
    }
    if (optionValues != null && (type != typeof(string) || optionNames == null
        || optionValues.Length != optionNames.Length || optionValues.Length == 0
        || optionValues.Any(x => x == null) || optionNames.Any(string.IsNullOrWhiteSpace)
        || optionValues.Distinct().Count() != optionValues.Length)) {
      throw new ArgumentException("Invalid external value options.");
    }
    var options = optionValues?.Select((value, index) => new DropdownItem {
        Value = value, Text = loc.T(optionNames[index]),
    }).ToArray();
    if (type == typeof(bool)) {
      options = [
          new DropdownItem { Value = "100", Text = loc.T(StateOnLocKey) },
          new DropdownItem { Value = "0", Text = loc.T(StateOffLocKey) },
      ];
    }
    var displayScale = percent ? 100 : 1;
    Definition = new ValueDef {
        ValueType = type == typeof(string) ? ScriptValue.TypeEnum.String : ScriptValue.TypeEnum.Number,
        DisplayNumericFormat = type == typeof(string) ? ValueDef.NumericFormatEnum.Unspecified
            : type == typeof(bool) ? ValueDef.NumericFormatEnum.Boolean
            : percent ? ValueDef.NumericFormatEnum.Percent
            : type == typeof(int) ? ValueDef.NumericFormatEnum.Integer : ValueDef.NumericFormatEnum.Float,
        DisplayNumericFormatRange = min.HasValue || max.HasValue
            ? ((float)(min * displayScale ?? double.NaN), (float)(max * displayScale ?? double.NaN)) : null,
        Options = options,
        RuntimeValueValidator = value => FromScript(value),
    };
  }

  /// <summary>Converts a provider value, rounding to two decimals and rejecting unrepresentable numbers.</summary>
  public ScriptValue ToScript(object value) {
    if (value == null || value.GetType() != _type) {
      throw new ScriptError.BadValue("Unexpected external value type.");
    }
    if (_type == typeof(string)) {
      ValidateString((string)value);
      return ScriptValue.FromString((string)value);
    }
    if (_type == typeof(bool)) {
      return ScriptValue.FromBool((bool)value);
    }
    var number = Convert.ToDouble(value);
    var scaled = Math.Round(number * 100, MidpointRounding.ToEven);
    if (double.IsNaN(number) || double.IsInfinity(number) || scaled < int.MinValue || scaled > int.MaxValue) {
      throw new ScriptError.ValueOutOfRange("External value exceeds the scripting numeric range.");
    }
    return ScriptValue.Of((int)scaled);
  }

  /// <summary>Converts to the declared provider type without enforcing editor-only numeric bounds.</summary>
  public object FromScript(ScriptValue value) {
    if (_type == typeof(string)) {
      ValidateString(value.AsString);
      return value.AsString;
    }
    if (_type == typeof(bool)) {
      return value.AsRawNumber != 0;
    }
    var number = _type == typeof(float) ? value.AsFloat : value.AsRawNumber / 100d;
    if (_type == typeof(int)) {
      if (value.AsRawNumber % 100 != 0) {
        throw new ScriptError.BadValue("An integer argument is required.");
      }
      return value.AsRawNumber / 100;
    }
    if (_type == typeof(float)) {
      return (float)number;
    }
    return number;
  }

  readonly Type _type;
  readonly string[] _options;

  void ValidateString(string value) {
    if (_options != null && !_options.Contains(value)) {
      throw new ScriptError.BadValue("Unknown external option: " + value);
    }
  }
}
