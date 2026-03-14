// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

class ExportRulesDialog(TemplatingService templatingService) : AbstractDialog {

  const string ExportRulesDialogAsset = "IgorZ.Automation/ExportRules";

  #region AbstractDialog implementation

  /// <inheritdoc/>
  protected override string DialogResourceName => ExportRulesDialogAsset;

  /// <inheritdoc/>
  protected override string CancelButtonName => null;

  /// <inheritdoc/>
  protected override string VerifyInput() => null;

  /// <inheritdoc/>
  protected override void ApplyInput() { }

  /// <inheritdoc/>
  protected override bool CheckHasChanges() => false;

  #endregion

  #region API

  public ExportRulesDialog WithActions(IList<IAutomationAction> actions) {
    _actions = actions;
    return this;
  }

  public override void Show() {
    base.Show();
    Root.Q<TextField>("ExportTextField").value = templatingService.RenderRulesToText(_actions);
  }

  public override void Close() {
    base.Close();
    _actions = null;
  }

  #endregion

  #region Implementation

  IList<IAutomationAction> _actions;

  #endregion
}
