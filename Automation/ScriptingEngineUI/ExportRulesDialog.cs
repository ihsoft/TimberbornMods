// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

class ExportRulesDialog : IPanelController {

  #region IPanelController implementation

  /// <inheritdoc/>
  public VisualElement GetPanel() {
    return _root;
  }

  /// <inheritdoc/>
  public bool OnUIConfirmed() {
    return true;
  }

  /// <inheritdoc/>
  public void OnUICancelled() {
    Close();
  }

  #endregion

  #region API

  public void Show(IList<IAutomationAction> actions) {
    _exportText.value = _templatingService.RenderRulesToText(actions);
    _panelStack.PushDialog(this);
  }

  public void Close() => _panelStack.Pop(this);

  #endregion

  #region Implementation

  readonly VisualElement _root;
  readonly PanelStack _panelStack;
  readonly TemplatingService _templatingService;

  readonly TextField _exportText;

  ExportRulesDialog(UiFactory uiFactory, PanelStack panelStack, TemplatingService templatingService) {
    _panelStack = panelStack;
    _templatingService = templatingService;

    _root = uiFactory.LoadVisualTreeAsset("IgorZ.Automation/ExportRules");
    _root.Q<Button>("CloseButton").clicked += Close;
    _root.Q<Button>("OkButton").clicked += Close;

    _exportText = _root.Q<TextField>("ExportTextField");
  }

  #endregion
}
