// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class SignalsEditorDialog : IPanelController {

  const string SignalsEditorDialogAsset = "IgorZ.Automation/SignalsEditor";
  const string SignalSourceTmplAsset = "IgorZ.Automation/SignalSourceTmpl";
  const string SignalTargetTmplAsset = "IgorZ.Automation/SignalTargetTmpl";

  #region IPanelController implementation

  /// <inheritdoc/>
  public VisualElement GetPanel() {
    return _root;
  }

  /// <inheritdoc/>
  public bool OnUIConfirmed() {
    SaveAndClose();
    return false;
  }

  void SaveAndClose() {
    //FIXME: check fro the right custom signal name
    foreach (var exportedSignal in _exportedSignals) {
      if (exportedSignal.customSignalField.value == "") {
        continue;
      }
      if (!ValidateCustomSignalName(exportedSignal.customSignalField.value)) {
        return;
      }
    }

    _rulesUiHelper.ClearSignalsOnBuilding();
    foreach (var exportedSignal in _exportedSignals) {
      if (exportedSignal.customSignalField.value == "") {
        continue;
      }
      _rulesUiHelper.SetExportedSignalName(exportedSignal.buildingSignalName, exportedSignal.customSignalField.value);
    }
    Close();
  }

  /// <inheritdoc/>
  public void OnUICancelled() {
    Close();
  }

  #endregion

  #region API

  ScriptingRulesUIHelper _rulesUiHelper;
  readonly List<(string buildingSignalName, TextField customSignalField)> _exportedSignals = [];

  /// <summary>Show this dialog. It blocks the game.</summary>
  public void Show(ScriptingRulesUIHelper rulesUIHelper, Action onSave) {
    _rulesUiHelper = rulesUIHelper;
    _root = _uiFactory.LoadVisualTreeAsset(SignalsEditorDialogAsset);
    _root.Q<Button>("ConfirmButton").clicked += () => OnUIConfirmed();
    _root.Q<Button>("CancelButton").clicked += Close;
    _root.Q<Button>("CloseButton").clicked += Close;

    var sourceSection = _root.Q2<VisualElement>("SourcesSection");
    sourceSection.Clear();
    var targetSection = _root.Q2<VisualElement>("TargetsSection");
    targetSection.Clear();
    _exportedSignals.Clear();
    foreach (var signalMapping in _rulesUiHelper.BuildingSignals) {
      var sourceTmpl = _uiFactory.LoadVisualElement(SignalSourceTmplAsset);
      sourceTmpl.Q2<Label>("SignalName").text = signalMapping.Describe;
      sourceSection.Add(sourceTmpl);

      var targetTmpl = _uiFactory.LoadVisualElement(SignalTargetTmplAsset);
      var targetField = targetTmpl.Q2<TextField>("SignalName");
      targetField.value = signalMapping.ExportedSignalName ?? "";
      targetTmpl.Q2<Button>("ClearSignalButton").clicked += () => targetField.value = "";
      targetSection.Add(targetTmpl);
      _exportedSignals.Add((signalMapping.SignalName, targetField));
    }

    _panelStack.PushDialog(this);
  }

  /// <summary>Hide this dialog. The game get's unblocked.</summary>
  public void Close() => _panelStack.Pop(this);

  #endregion

  #region Implementation

  static readonly Regex MappedSignalNamePattern = new(@"^(?!\.)([A-Za-z][A-Za-z0-9]+\.?)*([A-Za-z][A-Za-z0-9]*)$");

  VisualElement _root;
  
  readonly UiFactory _uiFactory;
  readonly PanelStack _panelStack;

  SignalsEditorDialog(UiFactory uiFactory, PanelStack panelStack) {
    _uiFactory = uiFactory;
    _panelStack = panelStack;
  }

  bool ValidateCustomSignalName(string name) {
    //FIXME: Don't log bad names. Highlight via UI.
    if (!MappedSignalNamePattern.IsMatch(name)) {
      DebugEx.Error("Invalid signal name: {0}", name);
      return false;
    }
    return true;
  }

  #endregion
}
