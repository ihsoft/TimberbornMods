// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.TimberDev.UI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

class ImportRulesDialog(TemplatingService templatingService) : AbstractDialog {

  const string ImportRulesDialogAsset = "IgorZ.Automation/ImportRules";

  const string ImportErrorLocKey = "IgorZ.Automation.Scripting.ImportRules.ImportError";
  const string ReadMoreUrlLocKey = "IgorZ.Automation.Scripting.ImportDialog.ReadMoreUrl";

  #region AbstractDialog implementation

  /// <inheritdoc/>
  protected override string DialogResourceName => ImportRulesDialogAsset;

  /// <inheritdoc/>
  protected override string VerifyInput() {
    try {
      templatingService.ParseFromText(
          _importText.value, _activeBuilding, _allowErrors.value, _skipFailedRules.value, out _);
    } catch (TemplatingService.ImportError e) {
      return UiFactory.T(ImportErrorLocKey, e.LineNum, e.Text);
    }
    return null;
  }

  /// <inheritdoc/>
  protected override void ApplyInput() {
    var rules = templatingService.ParseFromText(
        _importText.value, _activeBuilding, _allowErrors.value, _skipFailedRules.value, out var skippedRules);
    if (skippedRules > 0) {
      HostedDebugLog.Warning(_activeBuilding, "Skipped {0} rules during import", skippedRules);
    }
    var needToReplaceRules = Root.Q<Toggle>("DeleteExistingRulesToggle").value;
    if (needToReplaceRules) {
      _activeBuilding.ClearAllRules();
    }
    foreach (var rule in rules) {
      // Never add real rules, only clones.
      _activeBuilding.AddRule(rule.Condition.CloneDefinition(), rule.CloneDefinition());
    }
  }

  /// <inheritdoc/>
  protected override bool CheckHasChanges() => false;

  #endregion

  #region API

  public ImportRulesDialog WithBuilding(AutomationBehavior automationBehavior) {
    _activeBuilding = automationBehavior;
    return this;
  }

  public override void Show() {
    base.Show();

    Root.Q2<Button>("ReadMoreButton").clicked += () => Application.OpenURL(UiFactory.T(ReadMoreUrlLocKey));
    
    _skipFailedRules = Root.Q2<Toggle>("SkipFailedRulesToggle");
    _allowErrors = Root.Q2<Toggle>("AllowErrorsToggle");
    _allowErrors.RegisterValueChangedCallback(_ => _skipFailedRules.SetEnabled(!_allowErrors.value));
    _importText = Root.Q2<TextField>("ImportTextField");
    _importText.value = "";
  }

  public override void Close() {
    base.Close();
    _activeBuilding = null;
    _skipFailedRules = null;
    _allowErrors = null;
    _importText = null;
  }

  #endregion

  #region Implementation

  Toggle _allowErrors;
  Toggle _skipFailedRules;
  TextField _importText;

  AutomationBehavior _activeBuilding;

  #endregion
}
