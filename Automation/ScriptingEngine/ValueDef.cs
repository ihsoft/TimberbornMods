// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Text.RegularExpressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Definition of an argument that can be passed to a script action or be returned from a signal.</summary>
sealed record ValueDef {
  /// <summary>The type of the argument.</summary>
  public ScriptValue.TypeEnum ValueType { get; init; }

  /// <summary>
  /// Optional formatting processor. The result string should only be used for presenting the value, it can be
  /// non-parsable back to the value.
  /// </summary>
  /// <remarks>
  /// If provided, then any other formatting option is disregarded and only the result from this formatter is used.
  /// </remarks>
  /// <returns>
  /// The formatted value or <c>null</c>. In the latter case, the default formatting algorithm is resumed.
  /// </returns>
  public Func<ScriptValue, string> ValueFormatter { get; init; }

  /// <summary>Optional validating function for the argument value.</summary>
  /// <remarks>
  /// Normally, this check is performed in the runtime stage to verify the argument value. If the argument is a
  /// constant value, then this check is executed only once during the parsing stage.
  /// </remarks>
  /// <exception cref="ScriptError.RuntimeError">if value doesn't pass validation</exception>
  public Action<ScriptValue> ValueValidator { get; init; }

  /// <summary>Optional validating function for the value argument.</summary>
  /// <remarks>
  /// This check is performed in the parsing stage. Don't do value validation here! Use <see cref="ValueValidator"/>
  /// instead.
  /// </remarks>
  /// <exception cref="ScriptError.ParsingError">if argument is not appropriate</exception>
  public Action<IValueExpr> ArgumentValidator { get; init; }

  /// <summary>Optional list of pre-defined values for the argument.</summary>
  public DropdownItem<string>[] Options { get; init; }

  /// <summary>Integer value validation function.</summary>
  public static Action<ScriptValue> RangeCheckValidatorInt(int? min = null, int? max = null) {
    return value => {
      if (!max.HasValue) {
        if (value.AsInt < min) {
          throw new ScriptError.RuntimeError($"Value must be greater than or equal to {min}, found: {value.AsInt}");
        }
      } else if (!min.HasValue) {
        if (value.AsInt > max) {
          throw new ScriptError.RuntimeError($"Value must be less than or equal to {max}, found: {value.AsInt}");
        }
      } else {
        if (value.AsInt < min || value.AsInt > max) {
          throw new ScriptError.RuntimeError($"Value must be in range [{min}, {max}], found: {value.AsInt}");
        }
      }
    };
  }

  /// <summary>Float value validation function.</summary>
  public static Action<ScriptValue> RangeCheckValidatorFloat(float? min = null, float? max = null) {
    return value => {
      if (!max.HasValue) {
        if (value.AsNumber < Mathf.RoundToInt(min!.Value * 100f)) {
          throw new ScriptError.RuntimeError(
              $"Value must be greater than or equal to {min:F2}, found: {value.AsFloat:F2}");
        }
      } else if (!min.HasValue) {
        if (value.AsNumber > Mathf.RoundToInt(max.Value * 100f)) {
          throw new ScriptError.RuntimeError(
              $"Value must be less than or equal to {max:F2}, found: {value.AsFloat:F2}");
        }
      } else {
        if (value.AsNumber < Mathf.RoundToInt(min.Value * 100f)
            || value.AsNumber > Mathf.RoundToInt(max.Value * 100f)) {
          throw new ScriptError.RuntimeError($"Value must be in range [{min:F2}, {max:F2}], found: {value.AsFloat:F2}");
        }
      }
    };
  }

  /// <summary>String value validation function.</summary>
  public static Action<ScriptValue> PatternCheck(Regex pattern, string errorMessage) {
    return value => {
      if (!pattern.IsMatch(value.AsString)) {
        throw new ScriptError.RuntimeError($"Bad string value '{value.AsString}. {errorMessage}");
      }
    };
  }
}
