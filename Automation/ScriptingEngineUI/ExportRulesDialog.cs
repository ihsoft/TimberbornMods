// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

class ExportRulesDialog : AbstractDialog {

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
    Root.Q<TextField>("ExportTextField").value = _templatingService.RenderRulesToText(_actions);
  }

  public override void Close() {
    base.Close();
    _actions = null;
  }

  #endregion

  #region Implementation

  TemplatingService _templatingService;
  IList<IAutomationAction> _actions;

  /// <summary>Public for the inject to work properly.</summary>
  [Inject]
  public void InjectDependencies(TemplatingService templatingService) {
    _templatingService = templatingService;
  }

  #endregion

}
