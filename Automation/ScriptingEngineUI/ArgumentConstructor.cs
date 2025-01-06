// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ArgumentConstructor : BaseConstructor {

  public const string SignalTypeName = "-signal-";
  public const string NumberTypeName = "-number";

  public override VisualElement Root { get; }

  public enum ArgumentType {
    Signal,
    Number,
    String,
  }

  public ArgumentType SelectedType {
    get {
      return _typeSelectionDropdown.Value switch {
          SignalTypeName => ArgumentType.Signal,
          NumberTypeName => ArgumentType.Number,
          _ => ArgumentType.String
      };
    }
    set {
      _typeSelectionDropdown.Value = value switch {
          ArgumentType.Signal => SignalTypeName,
          ArgumentType.Number => NumberTypeName,
          ArgumentType.String => StringValue,
          _ => throw new InvalidOperationException("Unsupported argument type: " + value)
      };
    }
  }

  public string StringValue {
    get => _stringValue;
    set {
      _stringValue = value;
      if (SelectedType == ArgumentType.String) {
        _typeSelectionDropdown.Value = value;
        OnStringValueChanged?.Invoke(this, EventArgs.Empty);
      }
    }
  }
  string _stringValue;

  public int NumberValue {
    get => _numberValue;
    set {
      _numberValue = value;
      if (SelectedType == ArgumentType.Number) {
        _typeSelectionDropdown.Value = NumberTypeName;
        _textField.value = value.ToString();
      }
    }
  }
  int _numberValue;

  readonly SimpleDropdown<string> _typeSelectionDropdown;
  readonly TextField _textField;

  public event EventHandler OnStringValueChanged;

  public ArgumentConstructor(UiFactory uiFactory) : base(uiFactory) {
    _typeSelectionDropdown = uiFactory.CreateValueDropdown<string>((_, _) => UpdateTypeSelection());
    _textField = uiFactory.CreateTextField(width: 50);
    Root = MakeRow(_typeSelectionDropdown.DropdownElement, _textField);
  }

  public void SetDefinitions(DropdownItem<string>[] types) {
    _typeSelectionDropdown.Items = types;
    _typeSelectionDropdown.DropdownElement.ToggleDisplayStyle(types.Length > 1);
  }

  void UpdateTypeSelection() {
    DebugEx.Warning("*** UpdateTypeSelection: " + SelectedType);
    switch (SelectedType) {
      case ArgumentType.Signal:
        _textField.ToggleDisplayStyle(false);
        //FIXME: show signal selector
        DebugEx.Warning("*** show signal selector");
        break;
      case ArgumentType.Number:
        _textField.ToggleDisplayStyle(true);
        _textField.value = "";
        break;
      case ArgumentType.String:
        _textField.ToggleDisplayStyle(false);
        StringValue = _typeSelectionDropdown.Value;
        break;
      default:
        throw new InvalidOperationException("Unsupported argument type: " + SelectedType);
    }
  }
}
