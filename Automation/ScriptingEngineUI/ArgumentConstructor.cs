// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ArgumentConstructor : BaseConstructor {

  public const string InputTypeName = "-input-";
  const int ErrorStatusHighlightDurationMs = 1000;

  #region API

  public override VisualElement Root { get; }

  public event EventHandler OnStringValueChanged;

  public bool IsInput => _typeSelectionDropdown.SelectedValue == InputTypeName;

  public string Value {
    get => IsInput ? _textField.value : _typeSelectionDropdown.SelectedValue;
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

  public void SetDefinitions(DropdownItem<string>[] options) {
    _typeSelectionDropdown.Items = options;
    _typeSelectionDropdown.SetEnabled(options.Length > 1 || options[0].Value != InputTypeName);
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
      VisualEffects.SetTemporaryClass(_textField, ErrorStatusHighlightDurationMs, "text-field-error");
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
      VisualEffects.SetTemporaryClass(_textField, ErrorStatusHighlightDurationMs, "text-field-error");
    }
    return res;
  }

  #endregion

  #region Implementation 

  readonly ResizableDropdownElement _typeSelectionDropdown;
  readonly TextField _textField;

  public ArgumentConstructor(UiFactory uiFactory) : base(uiFactory) {
    _typeSelectionDropdown = uiFactory.CreateSimpleDropdown(_ => UpdateTypeSelection());
    _textField = uiFactory.CreateTextField(width: 100);
    Root = MakeRow(_typeSelectionDropdown, _textField);
  }

  void UpdateTypeSelection() {
    _textField.ToggleDisplayStyle(IsInput);
    _textField.value = "";
    OnStringValueChanged?.Invoke(this, EventArgs.Empty);
  }

  #endregion
}
