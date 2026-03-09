// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.Settings;
using IgorZ.TimberDev.UI;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

/// <summary>Definition of an argument that can be passed to a script action or be returned from a signal.</summary>
sealed record ValueDef {
  /// <summary>The type of the argument.</summary>
  public ScriptValue.TypeEnum ValueType { get; init; }

  /// <summary>
  /// The UI representation of a numeric value type. It defines how the players will see and enter the value.
  /// </summary>
  public enum NumericFormatEnum {
    /// <summary>The default value. It's not expect to have it for a number definition.</summary>
    /// <remarks>A numeric value definition with this value will result in a game crash.</remarks>
    /// <seealso cref="ValueDef"/>
    Unspecified,

    /// <summary>An integer value. No fractional part allowed.</summary>
    Integer,

    /// <summary>A float value with 2 digits after the comma.</summary>
    /// <remarks>If the number has more digits after the comme, the UI will block it.</remarks>
    Float,

    /// <summary>Percent value. Doesn't allow digits after the comma.</summary>
    /// <remarks>
    /// Internally, the percentile type is stored as a normalized float. That is, value "0.01" means "1%". The
    /// percentile type doesn't allow digits after the comma, because it would result in internal values with more than
    /// 2 digits after the comma, and this will be beyond what <see cref="ScriptValue"/> can store (2-digit fixed
    /// precision).
    /// </remarks>
    /// <seealso cref="ValueDef.DisplayNumericFormatRange"/>
    Percent,
  }

  /// <summary>Defines how the numeric value should be presented in UI.</summary>
  /// <remarks>
  /// This type is used by the formatters for output and parses of the input. This type is not used in runtime, and it
  /// doesn't affect expressions evaluation. It must be set if the value type is
  /// <see cref="ScriptValue.TypeEnum.Number"/>.
  /// </remarks>
  public NumericFormatEnum DisplayNumericFormat { get; init; }

  /// <summary>Defines the range of the numeric value. The values are inclusive.</summary>
  /// <summary>
  /// <p>
  /// It's used by the UI to show the range of the value and to validate the input. This range is not used in runtime,
  /// and it doesn't affect expressions evaluation. Use <c>float.NaN</c> to effectively disable the range check for the
  /// respected boundary.
  /// </p>
  /// <p>
  /// The values are checked in the form as they are entered! E.g. if it's a percent value, then the range check is
  /// performed for the value in percent, not for the normalized value. So, if you want to allow any percent value from
  /// 0% to 100%, you should set the range as [0, 100], not [0, 1] (as it will be stored in <see cref="ScriptValue"/>).
  /// </p>
  /// <p>To disable range checking leave this field uninitialized (default).</p>
  /// </summary>
  /// <seealso cref="DisplayNumericFormat"/>
  /// <seealso cref="ScriptValue.AsRawNumber"/>
  public (float min, float max) DisplayNumericFormatRange { get; init; }

  /// <summary>Optional validating function for the argument value.</summary>
  /// <remarks>
  /// This check is performed in the runtime stage to verify the argument value. The only exception is constants - for
  /// them, the check is made at the parsing time.
  /// </remarks>
  /// <exception cref="ScriptError.RuntimeError">if value doesn't pass validation</exception>
  /// <seealso cref="ScriptEngineSettings.CheckArgumentValues"/>
  public Action<ScriptValue> RuntimeValueValidator { get; init; }

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

  /// <summary>Float value validation function.</summary>
  /// <exception cref="ScriptError.RuntimeError">if value is out of range.</exception>
  public static Action<ScriptValue> RangeCheckValidator(float? min = null, float? max = null) {
    return value => {
      if (!max.HasValue) {
        if (value.AsRawNumber < Mathf.RoundToInt(min!.Value * 100f)) {
          throw new ScriptError.ValueOutOfRange(
              $"Value must be greater than or equal to {min:F2}, found: {value.AsFloat:F2}");
        }
      } else if (!min.HasValue) {
        if (value.AsRawNumber > Mathf.RoundToInt(max.Value * 100f)) {
          throw new ScriptError.ValueOutOfRange(
              $"Value must be less than or equal to {max:F2}, found: {value.AsFloat:F2}");
        }
      } else {
        if (value.AsRawNumber < Mathf.RoundToInt(min.Value * 100f)
            || value.AsRawNumber > Mathf.RoundToInt(max.Value * 100f)) {
          throw new ScriptError.ValueOutOfRange(
              $"Value must be in range [{min:F2}, {max:F2}], found: {value.AsFloat:F2}");
        }
      }
    };
  }
}
