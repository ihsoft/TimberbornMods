// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class SignalsEditorDialog : IPanelController {

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
    Close();
    _onSave?.Invoke();
  }

  /// <inheritdoc/>
  public void OnUICancelled() {
    Close();
  }
  Action _onSave;

  #endregion

  #region API

  const string SignalsEditorDialogAsset = "IgorZ.Automation/SignalsEditor";

  VisualElement _rowsContainer;
  ScriptingRulesUIHelper _rulesUiHelper;

  /// <summary>Show this dialog. It blocks the game.</summary>
  public void Show(ScriptingRulesUIHelper rulesUIHelper, Action onSave) {
    _rulesUiHelper = rulesUIHelper;
    _root = _uiFactory.LoadVisualTreeAsset(SignalsEditorDialogAsset);
    _rowsContainer = _root.Q2<VisualElement>("RowsContainer");
    _root.Q<Button>("ConfirmButton").clicked += () => OnUIConfirmed();
    _root.Q<Button>("CancelButton").clicked += Close;
    _root.Q<Button>("CloseButton").clicked += Close;

    var sourceSection = _root.Q2<VisualElement>("SourcesSection");
    sourceSection.Clear();
    var targetSection = _root.Q2<VisualElement>("TargetsSection");
    targetSection.Clear();
    foreach (var signal in _rulesUiHelper.BuildingSignals) {
      var sourceTmpl = _uiFactory.LoadVisualElement("IgorZ.Automation/SignalSourceTmpl");
      sourceTmpl.Q2<Label>("SignalName").text = signal.Describe;
      sourceSection.Add(sourceTmpl);
      var targetTmpl = _uiFactory.LoadVisualElement("IgorZ.Automation/SignalTargetTmpl");
      targetTmpl.Q2<TextField>("SignalName").value = signal.ExportedSignalName ?? "";
      targetSection.Add(targetTmpl);
    }

    _onSave = onSave;
    _panelStack.PushDialog(this);
  }

  /// <summary>Hide this dialog. The game get's unblocked.</summary>
  public void Close() => _panelStack.Pop(this);

  #endregion

  #region Implementation

  VisualElement _root;
  
  readonly UiFactory _uiFactory;
  readonly PanelStack _panelStack;

  SignalsEditorDialog(UiFactory uiFactory, PanelStack panelStack) {
    _uiFactory = uiFactory;
    _panelStack = panelStack;
  }

  #endregion
}
