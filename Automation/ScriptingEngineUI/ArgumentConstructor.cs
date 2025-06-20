// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ArgumentConstructor : BaseConstructor {

  public const string InputTypeName = "-input-";
  const int ErrorStatusHighlightDurationMs = 1000;

  #region API

  public override VisualElement Root { get; }

  public string Value {
    get => _typeSelectionDropdown.SelectedValue == InputTypeName
        ? _textField.value : _typeSelectionDropdown.SelectedValue;
    set {
      if (_typeSelectionDropdown.Items.Any(x => x.Value == value)) {
        _typeSelectionDropdown.SelectedValue = value;
        _textField.ToggleDisplayStyle(false);
      } else {
        _typeSelectionDropdown.SelectedValue = InputTypeName;
        _textField.value = value;
        _textField.ToggleDisplayStyle(true);
      }
    }
  }

  public void SetDefinition(ArgumentDefinition argumentDef) {
    _argumentDefinition = argumentDef;
    var options = _argumentDefinition.ValueOptions;
    _typeSelectionDropdown.Items = options;
    _typeSelectionDropdown.SetEnabled(options.Length > 1 || options[0].Value != InputTypeName);
    _hintText.text = argumentDef.ValueUiHint ?? "";
    _hintText.ToggleDisplayStyle(argumentDef.ValueUiHint != null);
  }

  public string GetScriptValue() {
    return _argumentDefinition.ValueType switch {
        ScriptValue.TypeEnum.Number => Mathf.RoundToInt(float.Parse(Value) * 100).ToString(),
        ScriptValue.TypeEnum.String => "'" + Value + "'",
        _ => throw new InvalidOperationException("Unknown argument type: " + _argumentDefinition.ValueType),
    };
  }

  public string Validate() {
    return _argumentDefinition.ValueType switch {
        ScriptValue.TypeEnum.Number => CheckInputForNumber(),
        ScriptValue.TypeEnum.String => CheckInputForString(),
        _ => throw new ArgumentException($"Invalid argument type: {_argumentDefinition.ValueType}")
    };
  }

  #endregion

  #region Implementation 

  static readonly Color ArgumentValueHintColor = new(0.5f, 0.5f, 0.5f);

  readonly ResizableDropdownElement _typeSelectionDropdown;
  readonly TextField _textField;
  readonly Label _hintText;
  ArgumentDefinition _argumentDefinition;

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

  string CheckInputForNumber() {
    string res = null;
    var value = _textField.value.Trim();
    if (value == "") {
      res = "Value must not be empty";
    } else if (!float.TryParse(value, out var floatValue)) {
      res = "Argument must be a number: " + value;
    } else if (_argumentDefinition.ValueValidator != null) {
      res = ExecuteValidator(_argumentDefinition.ValueValidator, floatValue);
    }
    if (res != null) {
      VisualEffects.SetTemporaryClass(_textField, ErrorStatusHighlightDurationMs, "text-field-error");
    }
    return res;
  }

  string CheckInputForString() {
    string res = null;
    var strValue = _textField.value;
    if (strValue.IndexOf('\'') >= 0) {
      res = "String must not contain single quotes: " + strValue;
    } else if (_argumentDefinition.ValueValidator != null) {
      res = ExecuteValidator(_argumentDefinition.ValueValidator, strValue);
    }
    if (res != null) {
      VisualEffects.SetTemporaryClass(_textField, ErrorStatusHighlightDurationMs, "text-field-error");
    }
    return res;
  }

  string ExecuteValidator<T>(Action<ScriptValue> validator, T value) {
    if (validator == null) {
      return null;
    }
    var scriptValue = value switch {
        float floatValue => ScriptValue.FromFloat(floatValue),
        string stringValue => ScriptValue.Of(stringValue),
        _ => throw new ArgumentException($"Unsupported value type: {typeof(T)}"),
    };
    try {
      validator(scriptValue);
    } catch (ScriptError.BadValue e) {
      return UIFactory.T(e.LocKey) + "\n" + e.Message;
    }
    return null;
  }

  #endregion
}
