// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ArgumentConstructor : BaseConstructor {

  const string ArgumentMaxValueHintLocKey = "IgorZ.Automation.Scripting.Editor.ArgumentMaxValueHint";
  const string ArgumentMinValueHintLocKey = "IgorZ.Automation.Scripting.Editor.ArgumentMinValueHint";

  public const string InputTypeName = "-input-";
  const int ErrorStatusHighlightDurationMs = 1000;

  #region API

  public override VisualElement Root { get; }

  public void SetScriptValue(ScriptValue scriptValue) {
    var valueDef = _argumentDefinition.ValueDef;
    var value = valueDef.ValueType switch {
        ScriptValue.TypeEnum.String => scriptValue.AsString,
        ScriptValue.TypeEnum.Number => valueDef.DisplayNumericFormat switch {
            ValueDef.NumericFormatEnum.Percent => scriptValue.AsRawNumber.ToString(),
            ValueDef.NumericFormatEnum.Float => scriptValue.AsFloat.ToString("0.00"),
            ValueDef.NumericFormatEnum.Integer => scriptValue.AsInt.ToString(),
            ValueDef.NumericFormatEnum.Unspecified => throw new InvalidOperationException($"Unsupported numeric format: {valueDef.DisplayNumericFormat}"),
        },
        ScriptValue.TypeEnum.Unset => throw new ArgumentException($"Invalid argument type: {valueDef.ValueType}"),
    };
    if (_typeSelectionDropdown.Items.Any(x => x.Value == value)) {
      _typeSelectionDropdown.SelectedValue = value;
      _textField.ToggleDisplayStyle(false);
    } else {
      _typeSelectionDropdown.SelectedValue = InputTypeName;
      _textField.value = value;
      _textField.ToggleDisplayStyle(true);
    }
  }

  public void SetDefinition(ArgumentDefinition argumentDef) {
    _argumentDefinition = argumentDef;
    var options = _argumentDefinition.ValueOptions;
    _typeSelectionDropdown.Items = options;
    _typeSelectionDropdown.SetEnabled(options.Length > 1 || options[0].Value != InputTypeName);
    var valueRange = argumentDef.ValueDef.DisplayNumericFormatRange;
    if (valueRange != default) {
      if (float.IsNaN(valueRange.min)) {
        _hintText.text = UIFactory.T(ArgumentMaxValueHintLocKey, FormatToArgPrecision(valueRange.max));
      } else if (float.IsNaN(valueRange.max)) {
        _hintText.text = UIFactory.T(ArgumentMinValueHintLocKey, FormatToArgPrecision(valueRange.min));
      } else {
        var minValue = FormatToArgPrecision(valueRange.min);
        var maxValue = FormatToArgPrecision(valueRange.max);
        _hintText.text = $"({minValue}..{maxValue})";
      }
    } else {
      _hintText.text = "";
    }
    _hintText.ToggleDisplayStyle(_hintText.text.Length > 0);
  }

  public string Validate() {
    string error;
    if (_argumentDefinition.ValueType == ScriptValue.TypeEnum.Number) {
      TryGetScriptValueAsNumber(out error);
    } else if (_argumentDefinition.ValueType == ScriptValue.TypeEnum.String) {
      TryGetScriptValueAsString(out error);
    } else {
      throw new ArgumentException($"Invalid argument type: {_argumentDefinition.ValueType}");
    }
    if (error != null) {
      SetErrorStatus();
    }
    return error;
  }

  /// <summary>Gets the value of the argument as a string that can be used in the generated script.</summary>
  /// <remarks>
  /// Make sure to call <see cref="Validate"/> before to ensure that the value is correct, otherwise the behavior is
  /// undefined (it can throw an exception or return an invalid value that will cause the script execution to fail).
  /// The method assumes that the value is already validated and doesn't perform any additional checks.
  /// </remarks>
  /// <returns>The value in a string form, suitable for consumption as a Lisp constant.</returns>
  /// <exception cref="InvalidOperationException">if the value type is unrecognized.</exception>
  public string GetScriptValue() {
    return _argumentDefinition.ValueType switch {
        ScriptValue.TypeEnum.Number => TryGetScriptValueAsNumber(out _).AsRawNumber.ToString(),
        ScriptValue.TypeEnum.String => $"'{TryGetScriptValueAsString(out _).AsString}'",
        ScriptValue.TypeEnum.Unset => throw new InvalidOperationException("Value type must be set"),
    };
  }

  #endregion

  #region Implementation 

  static readonly Color ArgumentValueHintColor = new(0.5f, 0.5f, 0.5f);
  static readonly Regex IsGoodFullFloatValueRegex = new(@"^-?\d+(\.[0-9]{1,2}[0]*)?$");
  static readonly Regex IsGoodSingleFloatValueRegex = new(@"^-?\d+(\.[0-9]?[0]*)?$");

  readonly ResizableDropdownElement _typeSelectionDropdown;
  readonly TextField _textField;
  readonly Label _hintText;
  ArgumentDefinition _argumentDefinition;

  string Value => _typeSelectionDropdown.SelectedValue == InputTypeName
      ? _textField.value
      : _typeSelectionDropdown.SelectedValue;

  public ArgumentConstructor(UiFactory uiFactory) : base(uiFactory) {
    _typeSelectionDropdown = uiFactory.CreateSimpleDropdown(_ => UpdateTypeSelection());
    _textField = uiFactory.CreateTextField(width: 100, classes: [UiFactory.GameTextBigClass]);
    _textField.style.height = Length.Percent(100);
    _hintText = uiFactory.CreateLabel(classes: [UiFactory.GameTextBigClass]);
    _hintText.style.color = ArgumentValueHintColor;
    Root = MakeRow(_typeSelectionDropdown, _textField, "", _hintText);
  }

  void UpdateTypeSelection() {
    _textField.ToggleDisplayStyle(_typeSelectionDropdown.SelectedValue == InputTypeName);
    _textField.value = "";
  }

  ScriptValue TryGetScriptValueAsNumber(out string error) {
    var strValue = Value.Trim();
    if (strValue == "") {
      error = "Argument must not be empty";
      return ScriptValue.InvalidValue;
    }
    var numericFormat = _argumentDefinition.ValueDef.DisplayNumericFormat;
    if (numericFormat == ValueDef.NumericFormatEnum.Unspecified) {
      throw new InvalidOperationException(
          $"Numeric format is not specified for argument definition: {_argumentDefinition}");
    }
    var valueDef = _argumentDefinition.ValueDef;

    // Integer & Percent.
    if (numericFormat is ValueDef.NumericFormatEnum.Integer or ValueDef.NumericFormatEnum.Percent) {
      if (!int.TryParse(strValue, out var intValue)) {
        error = $"Value must be an integer: {strValue}";
        return ScriptValue.InvalidValue;
      }
      var range = valueDef.DisplayNumericFormatRange;
      if (range != default) {
        if (!float.IsNaN(range.min) && range.min > intValue) {
          error = $"Value must be greater than or equal to {range.min}, found: {intValue}";
          return ScriptValue.InvalidValue;
        }
        if (!float.IsNaN(range.max) && range.max < intValue) {
          error = $"Value must be less than or equal to {range.max}, found: {intValue}";
          return ScriptValue.InvalidValue;
        }
      }
      var result = numericFormat == ValueDef.NumericFormatEnum.Integer
          ? ScriptValue.FromInt(intValue)
          : ScriptValue.Of(intValue);
      return ExecuteValidator(result, out error) ? result : ScriptValue.InvalidValue;
    }

    // Float types. They differ by how many digits after the comma they can have (max is 2).
    if (numericFormat is ValueDef.NumericFormatEnum.Float) {
      if (!float.TryParse(strValue, out var floatValue)) {
        error = $"Value must be an float: {strValue}";
        return ScriptValue.InvalidValue;
      }
      if (!IsGoodFullFloatValueRegex.IsMatch(strValue)) {
        error = $"Value must not have more than 2 digits after the comma: {strValue}";
        return ScriptValue.InvalidValue;
      }
      var range = valueDef.DisplayNumericFormatRange;
      if (range != default) {
        if (!float.IsNaN(range.min) && range.min > floatValue) {
          error = $"Value must be greater than or equal to {range.min}, found: {floatValue}";
          return ScriptValue.InvalidValue;
        }
        if (!float.IsNaN(range.max) && range.max < floatValue) {
          error = $"Value must be less than or equal to {range.max}, found: {floatValue}";
          return ScriptValue.InvalidValue;
        }
      }
      var result = ScriptValue.FromFloat(floatValue);  // If the value is too large, it will be silently rounded!
      return ExecuteValidator(result, out error) ? result : ScriptValue.InvalidValue;
    }
    // We should never end up here.
    throw new InvalidOperationException($"Unsupported numeric format: {numericFormat}");
  }

  ScriptValue TryGetScriptValueAsString(out string error) {
    var strValue = Value.Trim();
    if (strValue.IndexOf('\'') >= 0) {
      error = $"String must not contain single quotes: {strValue}";
      return ScriptValue.InvalidValue;
    }
    // Skip checking for options since if it's not possible to enter custom value when there are options defined.
    var result = ScriptValue.FromString(strValue);
    return ExecuteValidator(result, out error) ? result : ScriptValue.InvalidValue;
  }

  bool ExecuteValidator(ScriptValue value, out string error) {
    var validator = _argumentDefinition.ValueDef.RuntimeValueValidator;
    if (validator != null) {
      try {
        validator(value);
      } catch (ScriptError.BadValue e) {
        error = e.Message;
        return false;
      }
    }
    error = null;
    return true;
  }

  string FormatToArgPrecision(float value) {
    return _argumentDefinition.ValueDef.DisplayNumericFormat switch {
        ValueDef.NumericFormatEnum.Float => value.ToString("0.00"),
        ValueDef.NumericFormatEnum.Integer or ValueDef.NumericFormatEnum.Percent =>
            value.ToString(CultureInfo.InvariantCulture),
        ValueDef.NumericFormatEnum.Unspecified => throw new InvalidOperationException("Numeric format must be set"),
    };
  }

  void SetErrorStatus() {
    VisualEffects.SetTemporaryClass(_textField, ErrorStatusHighlightDurationMs, "text-field-error");
  }

  #endregion
}
