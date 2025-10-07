// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Text.RegularExpressions;
using IgorZ.TimberDev.UI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class SignalsEditorDialog : AbstractDialog {

  const string SignalsEditorDialogAsset = "IgorZ.Automation/SignalsEditor";
  const string SignalSourceTmplAsset = "IgorZ.Automation/SignalSourceTmpl";
  const string SignalTargetTmplAsset = "IgorZ.Automation/SignalTargetTmpl";

  const string BadSignalNameMsgLocKey = "IgorZ.Automation.Scripting.SignalsEditor.BadSignalNameMsg";

  #region AbstractDialog implementation

  /// <inheritdoc/>
  protected override string DialogResourceName => SignalsEditorDialogAsset;

  /// <inheritdoc/>
  protected override string VerifyInput() {
    //FIXME: Also highlight bad names via UI.
    foreach (var exportedSignal in _exportedSignals) {
      if (exportedSignal.customSignalField.value == "") {
        continue;
      }
      if (!ValidateCustomSignalName(exportedSignal.customSignalField.value)) {
        return UiFactory.T(BadSignalNameMsgLocKey);
      }
    }
    return null;
  }

  /// <inheritdoc/>
  protected override void ApplyInput() {
    _rulesUiHelper.ClearSignalsOnBuilding();
    foreach (var exportedSignal in _exportedSignals) {
      if (exportedSignal.customSignalField.value == "") {
        continue;
      }
      _rulesUiHelper.SetExportedSignalName(exportedSignal.buildingSignalName, exportedSignal.customSignalField.value);
    }
  }

  /// <inheritdoc/>
  protected override bool CheckHasChanges() {
    //FIXME: actually verify changes.
    return false;
  }

  #endregion

  #region API

  ScriptingRulesUIHelper _rulesUiHelper;
  readonly List<(string buildingSignalName, TextField customSignalField)> _exportedSignals = [];

  public void Initialize(ScriptingRulesUIHelper rulesUIHelper) {
    _rulesUiHelper = rulesUIHelper;

    var sourceSection = Root.Q2<VisualElement>("SourcesSection");
    sourceSection.Clear();
    var targetSection = Root.Q2<VisualElement>("TargetsSection");
    targetSection.Clear();
    _exportedSignals.Clear();
    foreach (var signalMapping in _rulesUiHelper.BuildingSignals) {
      var sourceTmpl = UiFactory.LoadVisualElement(SignalSourceTmplAsset);
      sourceTmpl.Q2<Label>("SignalName").text = signalMapping.Describe;
      sourceSection.Add(sourceTmpl);

      var targetTmpl = UiFactory.LoadVisualElement(SignalTargetTmplAsset);
      var targetField = targetTmpl.Q2<TextField>("SignalName");
      targetField.value = signalMapping.ExportedSignalName ?? "";
      targetTmpl.Q2<Button>("ClearSignalButton").clicked += () => targetField.value = "";
      targetSection.Add(targetTmpl);
      _exportedSignals.Add((signalMapping.SignalName, targetField));
    }
  }

  #endregion

  #region Implementation

  static readonly Regex MappedSignalNamePattern = new(@"^(?!\.)([A-Za-z][A-Za-z0-9]+\.?)*([A-Za-z][A-Za-z0-9]*)$");
  
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
