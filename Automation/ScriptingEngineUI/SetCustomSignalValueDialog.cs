// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class SetCustomSignalValueDialog(SignalDispatcher signalDispatcher) : AbstractDialog {

  const string DialogAsset = "IgorZ.Automation/SetCustomSignalValue";
  const string SignalNamePrefix = "Signals.";

  const string BadSignalNameMsgLocKey = "IgorZ.Automation.Scripting.SignalsEditor.BadSignalNameMsg";
  const string DuplicateSignalNameLocKey = "IgorZ.Automation.PinnedCustomSignals.SetValue.DuplicateSignalName";
  const string InvalidValueFormatLocKey = "IgorZ.Automation.PinnedCustomSignals.SetValue.InvalidValueFormat";
  const string InvalidValueNumberLocKey = "IgorZ.Automation.PinnedCustomSignals.SetValue.InvalidValueNumber";
  const string SignalNameLocKey = "IgorZ.Automation.PinnedCustomSignals.SetValue.SignalName";

  const string BadInputClass = "text-field-error";

  #region AbstractDialog implementation

  /// <inheritdoc/>
  protected override string DialogResourceName => DialogAsset;

  /// <inheritdoc/>
  protected override string VerifyInput() {
    var nameError = ValidateSignalName();
    if (nameError != null) {
      return nameError;
    }
    var valueError = ValidateSignalValue();
    if (valueError != null) {
      return valueError;
    }
    return null;
  }

  /// <inheritdoc/>
  protected override void ApplyInput() {
    var rawValue = ScriptValue.FromFloat(float.Parse(_valueField.value)).AsRawNumber;
    if (_canEditName) {
      _signalName = SignalNamePrefix + _signalNameField.value.Trim();
    }
    signalDispatcher.SetManualSignalValue(_signalName, rawValue);
    _onSignalSaved?.Invoke(_signalName);
  }

  /// <inheritdoc/>
  protected override bool CheckHasChanges() {
    return _valueField.value != _originalValue
        || _canEditName && _signalNameField.value.Trim() != _originalSignalName;
  }

  #endregion

  #region API

  public SetCustomSignalValueDialog WithSignal(string signalName, string displayName, int rawValue) {
    _signalName = signalName;
    _displayName = displayName;
    _originalSignalName = displayName;
    _originalValue = ScriptValue.Of(rawValue).AsFloat.ToString("0.##");
    _canEditName = false;
    return this;
  }

  public SetCustomSignalValueDialog WithNewSignal(IEnumerable<string> existingSignals, Action<string> onSignalSaved) {
    _signalName = null;
    _displayName = null;
    _originalSignalName = "";
    _originalValue = "0";
    _canEditName = true;
    _existingSignals = existingSignals.ToHashSet();
    _onSignalSaved = onSignalSaved;
    return this;
  }

  public override void Show() {
    base.Show();
    var signalNameLabel = Root.Q2<Label>("SignalName");
    var signalNameInputWrapper = Root.Q2<VisualElement>("SignalNameInputWrapper");
    _signalNameField = Root.Q2<TextField>("SignalNameInput");
    signalNameLabel.ToggleDisplayStyle(!_canEditName);
    signalNameInputWrapper.ToggleDisplayStyle(_canEditName);
    signalNameLabel.text = UiFactory.T(SignalNameLocKey, _displayName);
    _signalNameField.value = _originalSignalName;
    _signalNameField.RegisterCallback<ChangeEvent<string>>(_ => UpdateSignalNameValidation());
    _valueField = Root.Q2<TextField>("SignalValue");
    _valueField.value = _originalValue;
    _valueField.RegisterCallback<ChangeEvent<string>>(_ => UpdateValueValidation());
    UpdateSignalNameValidation();
    UpdateValueValidation();
    if (_canEditName) {
      _signalNameField.Focus();
    } else {
      _valueField.Focus();
    }
  }

  public override void Close() {
    base.Close();
    _signalName = null;
    _displayName = null;
    _originalSignalName = null;
    _originalValue = null;
    _canEditName = false;
    _existingSignals = [];
    _onSignalSaved = null;
    _signalNameField = null;
    _valueField = null;
  }

  #endregion

  #region Implementation

  static readonly Regex IsGoodSignalValueRegex = new(@"^-?\d+(\.[0-9]{1,2}[0]*)?$");

  void UpdateSignalNameValidation() {
    _signalNameField.EnableInClassList(BadInputClass, ValidateSignalName() != null);
  }

  void UpdateValueValidation() {
    _valueField.EnableInClassList(BadInputClass, ValidateSignalValue() != null);
  }

  string ValidateSignalName() {
    if (!_canEditName) {
      return null;
    }
    var signalName = _signalNameField.value.Trim();
    try {
      SymbolExpr.CheckName(signalName);
    } catch (ScriptError.ParsingError) {
      return UiFactory.T(BadSignalNameMsgLocKey);
    }
    var fullSignalName = SignalNamePrefix + signalName;
    if (_existingSignals.Contains(fullSignalName)
        || signalDispatcher.GetRegisteredSignals().Contains(fullSignalName)) {
      return UiFactory.T(DuplicateSignalNameLocKey, signalName);
    }
    return null;
  }

  string ValidateSignalValue() {
    var value = _valueField.value.Trim();
    if (!float.TryParse(value, out _)) {
      return UiFactory.T(InvalidValueNumberLocKey, value);
    }
    if (!IsGoodSignalValueRegex.IsMatch(value)) {
      return UiFactory.T(InvalidValueFormatLocKey, value);
    }
    return null;
  }

  string _signalName;
  string _displayName;
  string _originalSignalName;
  string _originalValue;
  bool _canEditName;
  HashSet<string> _existingSignals = [];
  Action<string> _onSignalSaved;
  TextField _signalNameField;
  TextField _valueField;

  #endregion
}
