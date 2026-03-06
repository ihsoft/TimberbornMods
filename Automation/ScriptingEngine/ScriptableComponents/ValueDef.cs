// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

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
  /// This check is performed in the runtime stage to verify the argument value. The only exception is constants - for
  /// them, the check is made at the parsing time.
  /// </remarks>
  /// <exception cref="ScriptError.RuntimeError">if value doesn't pass validation</exception>
  /// <seealso cref="ScriptEngineSettings.CheckArgumentValues"/>
  public Action<ScriptValue> RuntimeValueValidator { get; init; }

  /// <summary>Optional hint text to show in UI for the argument.</summary>
  public string ValueUiHint { get; init; }

  /// <summary>Optional validating function for the argument value <i>expression</i>.</summary>
  /// <remarks>
  /// This check is performed at the parsing stage. Don't do value validation here! Use
  /// <see cref="RuntimeValueValidator"/> instead. The main purpose of this function is to validate if the argument
  /// makes sense from the parsing standpoint. E.g. if it's a string literal when it's the only kind of values allowed.
  /// </remarks>
  /// <exception cref="ScriptError.ParsingError">if argument is not appropriate</exception>
  public Action<IValueExpr> ArgumentValidator { get; init; }

  /// <summary>Optional list of pre-defined values for the argument.</summary>
  public DropdownItem<string>[] Options { get; init; }

  /// <summary>Map of replacement options for the argument.</summary>
  /// <remarks>Used to replace deprecated constants with the new values.</remarks>
  public Dictionary<string, string> CompatibilityOptions { get; init; }

  /// <summary>Integer value validation function.</summary>
  /// <exception cref="ScriptError.RuntimeError">if value is out of range.</exception>
  public static Action<ScriptValue> RangeCheckValidatorInt(int? min = null, int? max = null) {
    return value => {
      if (value.AsNumber % 100 != 0) {
        throw new ScriptError.ValueOutOfRange($"Value must be an integer, found: {value.AsFloat:F2}");
      }
      if (!max.HasValue) {
        if (value.AsInt < min) {
          throw new ScriptError.ValueOutOfRange($"Value must be greater than or equal to {min}, found: {value.AsInt}");
        }
      } else if (!min.HasValue) {
        if (value.AsInt > max) {
          throw new ScriptError.ValueOutOfRange($"Value must be less than or equal to {max}, found: {value.AsInt}");
        }
      } else {
        if (value.AsInt < min || value.AsInt > max) {
          throw new ScriptError.ValueOutOfRange($"Value must be in range [{min}, {max}], found: {value.AsInt}");
        }
      }
    };
  }

  /// <summary>Float value validation function.</summary>
  /// <exception cref="ScriptError.RuntimeError">if value is out of range.</exception>
  public static Action<ScriptValue> RangeCheckValidatorFloat(float? min = null, float? max = null) {
    return value => {
      if (!max.HasValue) {
        if (value.AsNumber < Mathf.RoundToInt(min!.Value * 100f)) {
          throw new ScriptError.ValueOutOfRange(
              $"Value must be greater than or equal to {min:F2}, found: {value.AsFloat:F2}");
        }
      } else if (!min.HasValue) {
        if (value.AsNumber > Mathf.RoundToInt(max.Value * 100f)) {
          throw new ScriptError.ValueOutOfRange(
              $"Value must be less than or equal to {max:F2}, found: {value.AsFloat:F2}");
        }
      } else {
        if (value.AsNumber < Mathf.RoundToInt(min.Value * 100f)
            || value.AsNumber > Mathf.RoundToInt(max.Value * 100f)) {
          throw new ScriptError.ValueOutOfRange(
              $"Value must be in range [{min:F2}, {max:F2}], found: {value.AsFloat:F2}");
        }
      }
    };
  }
}
