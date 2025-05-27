// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

class ImportRulesDialog : IPanelController {

  const string ImportErrorLocKey = "IgorZ.Automation.Scripting.ImportRules.ImportError";
  const string ReadMoreUrlLocKey = "IgorZ.Automation.Scripting.ImportDialog.ReadMoreUrl";

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

  public bool DeleteExistingRules => _root.Q<Toggle>("DeleteExistingRulesToggle").value;

  public void Show(AutomationBehavior automationBehavior, Action<IList<IAutomationAction>> onSave) {
    _activeBuilding = automationBehavior;
    _onSave = onSave;
    _importText.value = "";
    _panelStack.PushDialog(this);
  }

  public void Close() => _panelStack.Pop(this);

  #endregion

  #region Implementation

  readonly VisualElement _root;
  readonly UiFactory _uiFactory;
  readonly PanelStack _panelStack;
  readonly DialogBoxShower _dialogBoxShower;
  readonly TemplatingService _templatingService;

  readonly Toggle _allowErrors;
  readonly Toggle _skipFailedRules;
  readonly TextField _importText;

  AutomationBehavior _activeBuilding;
  Action<IList<IAutomationAction>> _onSave;

  ImportRulesDialog(UiFactory uiFactory, PanelStack panelStack, DialogBoxShower dialogBoxShower,
                    TemplatingService templatingService) {
    _uiFactory = uiFactory;
    _panelStack = panelStack;
    _dialogBoxShower = dialogBoxShower;
    _templatingService = templatingService;

    _root = _uiFactory.LoadVisualTreeAsset("IgorZ.Automation/ImportRules");
    _root.Q<Button>("CloseButton").clicked += Close;
    _root.Q<Button>("CancelButton").clicked += Close;
    _root.Q<Button>("ImportButton").clicked += OnSaveButtonClicked;
    _root.Q<Button>("ReadMoreButton").clicked += () => Application.OpenURL(_uiFactory.T(ReadMoreUrlLocKey));
    
    _skipFailedRules = _root.Q<Toggle>("SkipFailedRulesToggle");
    _allowErrors = _root.Q<Toggle>("AllowErrorsToggle");
    _allowErrors.RegisterValueChangedCallback(evt => _skipFailedRules.SetEnabled(!_allowErrors.value));
    _importText = _root.Q<TextField>("ImportTextField");
  }

  void OnSaveButtonClicked() {
    List<IAutomationAction> rules;
    var skippedRules = 0;
    try {
      rules = _templatingService.ParseFromText(
          _importText.value, _activeBuilding, _allowErrors.value, _skipFailedRules.value, out skippedRules);
    } catch (TemplatingService.ImportError e) {
      _dialogBoxShower.Create().SetMessage(_uiFactory.T(ImportErrorLocKey, e.LineNum, e.Text)).Show();
      return;
    }
    if (skippedRules > 0) {
      HostedDebugLog.Warning(_activeBuilding, "Skipped {0} rules during import", skippedRules);
    }
    _onSave?.Invoke(rules);
    Close();
  }

  #endregion
}
