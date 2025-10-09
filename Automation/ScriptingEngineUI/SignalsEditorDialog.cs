// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class SignalsEditorDialog : AbstractDialog {

  const string SignalsEditorDialogAsset = "IgorZ.Automation/SignalsEditor";
  const string SignalSourceTmplAsset = "IgorZ.Automation/SignalSourceTmpl";
  const string SignalTargetTmplAsset = "IgorZ.Automation/SignalTargetTmpl";

  const string BadSignalNameMsgLocKey = "IgorZ.Automation.Scripting.SignalsEditor.BadSignalNameMsg";

  const string SignalNameModifiedClass = "text-field-modified";
  const string BadSignalNameClass = "text-field-error";

  #region AbstractDialog implementation

  /// <inheritdoc/>
  protected override string DialogResourceName => SignalsEditorDialogAsset;

  /// <inheritdoc/>
  protected override string VerifyInput() {
    return _mappingLines.Any(x => x.HasError) ? UiFactory.T(BadSignalNameMsgLocKey) : null;
  }

  /// <inheritdoc/>
  protected override void ApplyInput() {
    _rulesUiHelper.ClearSignalsOnBuilding();
    foreach (var exportedSignal in _mappingLines) {
      if (exportedSignal.CustomSignalField.value == "") {
        continue;
      }
      _rulesUiHelper.SetExportedSignalName(exportedSignal.Signal.SignalName, exportedSignal.CustomSignalField.value);
    }
  }

  /// <inheritdoc/>
  protected override bool CheckHasChanges() {
    return _mappingLines.Any(exportedSignal => exportedSignal.HasChanges);
  }

  #endregion

  #region API

  public SignalsEditorDialog WithUiHelper(ScriptingRulesUIHelper rulesUIHelper) {
    _rulesUiHelper = rulesUIHelper;
    return this;
  }

  /// <inheritdoc/>
  public override void Show() {
    base.Show();

    var sourceSection = Root.Q2<VisualElement>("SourcesSection");
    sourceSection.Clear();
    var targetSection = Root.Q2<VisualElement>("TargetsSection");
    targetSection.Clear();

    _mappingLines.Clear();
    foreach (var signalMapping in _rulesUiHelper.BuildingSignals) {
      var sourceTmpl = UiFactory.LoadVisualElement(SignalSourceTmplAsset);
      sourceTmpl.Q2<Label>("SignalName").text = signalMapping.Describe;
      sourceSection.Add(sourceTmpl);

      var targetTmpl = UiFactory.LoadVisualElement(SignalTargetTmplAsset);
      var targetField = targetTmpl.Q2<TextField>("SignalName");
      var mappingLine = new MappingLine(signalMapping, targetField);
      targetField.value = signalMapping.ExportedSignalName ?? "";
      targetTmpl.Q2<Button>("ClearSignalButton").clicked += () => targetField.value = "";
      targetSection.Add(targetTmpl);
      _mappingLines.Add(mappingLine);
    }
  }

  /// <inheritdoc/>
  public override void Close() {
    base.Close();
    _mappingLines.Clear();
    _rulesUiHelper = null;
  }

  #endregion

  #region Implementation

  static readonly Regex MappedSignalNamePattern = new(@"^(?!\.)([A-Za-z][A-Za-z0-9]+\.?)*([A-Za-z][A-Za-z0-9]*)$");

  ScriptingRulesUIHelper _rulesUiHelper;
  readonly List<MappingLine> _mappingLines = [];

  readonly record struct MappingLine {
    public MappingLine(ScriptingRulesUIHelper.BuildingSignal Signal, TextField CustomSignalField) {
      this.Signal = Signal;
      this.CustomSignalField = CustomSignalField;
      CustomSignalField.RegisterCallback<ChangeEvent<string>>(ValidateSignalNameCallback);
    }

    public bool HasChanges => CustomSignalField.ClassListContains(SignalNameModifiedClass);
    public bool HasError => CustomSignalField.ClassListContains(BadSignalNameClass);
    public ScriptingRulesUIHelper.BuildingSignal Signal { get; }
    public TextField CustomSignalField { get; }

    void ValidateSignalNameCallback(ChangeEvent<string> evt) {
      ValidateSignalName(evt.newValue);
    }

    void ValidateSignalName(string newValue) {
      if (newValue == "" || MappedSignalNamePattern.IsMatch(newValue)) {
        CustomSignalField.RemoveFromClassList(BadSignalNameClass);
        if ((Signal.ExportedSignalName ?? "") != newValue) {
          CustomSignalField.AddToClassList(SignalNameModifiedClass);
        } else {
          CustomSignalField.RemoveFromClassList(SignalNameModifiedClass);
        }
      } else {
        CustomSignalField.RemoveFromClassList(SignalNameModifiedClass);
        CustomSignalField.AddToClassList(BadSignalNameClass);
      }
    }
  }

  #endregion
}
