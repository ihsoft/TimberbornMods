// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ArgumentConstructor : BaseConstructor {

  public const string InputTypeName = "-input-";
  const int ErrorStatusHighlightDurationMs = 1000;

  public override VisualElement Root { get; }

  public bool IsInput => _typeSelectionDropdown.Value == InputTypeName;

  public string Value {
    get => IsInput ? _textField.value : _typeSelectionDropdown.Value;
    set {
      if (_typeSelectionDropdown.Items.Any(x => x.Value == value)) {
        _typeSelectionDropdown.Value = value;
        _textField.ToggleDisplayStyle(false);
      } else {
        _typeSelectionDropdown.Value = InputTypeName;
        _textField.value = value;
        _textField.ToggleDisplayStyle(true);
      }
    }
  }

  public event EventHandler OnStringValueChanged;

  readonly SimpleDropdown<string> _typeSelectionDropdown;
  readonly TextField _textField;

  public ArgumentConstructor(UiFactory uiFactory) : base(uiFactory) {
    _typeSelectionDropdown = uiFactory.CreateValueDropdown<string>((_, _) => UpdateTypeSelection());
    //FIXME: pass text size.
    _textField = uiFactory.CreateTextField(width: 100);
    Root = MakeRow(_typeSelectionDropdown.DropdownElement, _textField);
  }

  public void SetDefinitions(DropdownItem<string>[] options) {
    _typeSelectionDropdown.Items = options;
    _typeSelectionDropdown.DropdownElement.SetEnabled(options.Length > 1 || options[0].Value != InputTypeName);
  }

  public string CheckInputForNumber(Func<float, string> check = null) {
    string res = null;
    var value = _textField.value.Trim();
    if (value == "") {
      res = "Argument must be a number";
    } else if (!float.TryParse(value, out var val)) {
      res = "Argument must be a number: " + value;
    } else if (check != null) {
      res = check(val);
    }
    if (res != null) {
      VisualEffects.ScheduleSwitchEffect(
          _textField, ErrorStatusHighlightDurationMs, new Color(1, 0, 0, 0.2f), Color.clear,
          (f, c) => f.textInput.style.backgroundColor = c);
    } else {
      _textField.style.backgroundColor = UiFactory.DefaultColor;
    }
    return res;
  }

  public string CheckInputForString(Func<string, string> check = null) {
    string res = null;
    var value = _textField.value;
    if (value.IndexOf('\'') >= 0) {
      res = "String must not contain single quotes: " + value;
    } else if (check != null) {
      res = check(value);
    }
    if (res != null) {
      VisualEffects.ScheduleSwitchEffect(
          _textField, ErrorStatusHighlightDurationMs, Color.red, UiFactory.DefaultColor,
          (f, c) => f.style.backgroundColor = c);
    } else {
      _textField.style.backgroundColor = UiFactory.DefaultColor;
    }
    return res;
  }

  void UpdateTypeSelection() {
    _textField.ToggleDisplayStyle(IsInput);
    _textField.value = "";
  }
}
