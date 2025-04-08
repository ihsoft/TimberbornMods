// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RulesEditorDialog : IPanelController {

  const string PendingEditsNotificationLocKey = "IgorZ.Automation.Scripting.Editor.PendingEditsNotification";
  const string UnsavedChangesConfirmationLocKey = "IgorZ.Automation.Scripting.Editor.UnsavedChangesConfirmation";
  const string ReadMoreLinkLocKey = "IgorZ.Automation.Scripting.Editor.ReadMoreLink";

  #region IPanelController implementation

  /// <inheritdoc/>
  public VisualElement GetPanel() {
    return _root;
  }

  /// <inheritdoc/>
  public bool OnUIConfirmed() {
    if (EditsPending) {
      ShowPendingEditsNotification();
      return true;
    }
    SaveAndClose();
    return false;
  }

  void SaveAndClose() {
    SaveRulesAndCloseDialog();
    Close();
    _onClosed?.Invoke();
  }

  /// <inheritdoc/>
  public void OnUICancelled() {
    if (RulesChanged || EditsPending) {
      ShowUnsavedChangesConfirmation(Close);
      return;
    }
    Close();
  }
  Action _onClosed;

  #endregion

  #region API

  public void Show(AutomationBehavior behavior, Action onClosed) {
    _root = _uiFactory.LoadVisualTreeAsset("IgorZ.Automation/RulesEditor");
    _ruleRowsContainer = _root.Q<VisualElement>("RuleRowsContainer");
    _root.Q<Button>("ConfirmButton").clicked += () => OnUIConfirmed();
    _root.Q<Button>("CancelButton").clicked += Close;
    _root.Q<Button>("CloseButton").clicked += Close;
    _root.Q<Button>("MoreInfoButton").clicked += () => Application.OpenURL(_uiFactory.T(ReadMoreLinkLocKey));

    var buttons = _root.Q("Buttons");
    buttons.Clear();
    foreach (var provider in _editorProviders) {
      var btn = _uiFactory.LoadVisualElement<Button>("IgorZ.Automation/RulesEditorButtonTmpl");
      btn.text = _uiFactory.T(provider.CreateRuleLocKey);
      btn.clicked += () => provider.MakeForRule(CreateScriptedRule());
      buttons.Add(btn);
    }
    SetActiveBuilding(behavior);

    _onClosed = onClosed;
    _panelStack.PushDialog(this);
  }

  public void Close() => _panelStack.Pop(this);

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  readonly PanelStack _panelStack;
  readonly DialogBoxShower _dialogBoxShower;

  VisualElement _root;

  readonly List<RuleRow> _ruleRows = []; 
  readonly IEditorProvider[] _editorProviders;

  bool RulesChanged => _ruleRows.Any(x => x.IsDeleted || x.IsModified);
  bool EditsPending => _ruleRows.Any(x => x.IsInEditMode);

  VisualElement _ruleRowsContainer;
  AutomationBehavior _activeBuilding;

  RulesEditorDialog(UiFactory uiFactory, PanelStack panelStack, DialogBoxShower dialogBoxShower,
                    ScriptEditorProvider scriptEditorProvider,
                    ConstructorEditorProvider constructorEditorProvider) {
    _uiFactory = uiFactory;
    _panelStack = panelStack;
    _dialogBoxShower = dialogBoxShower;
    _editorProviders = [scriptEditorProvider, constructorEditorProvider];
  }

  void Reset() {
    _ruleRowsContainer.Clear();
    _ruleRows.Clear();
  }

  void ShowUnsavedChangesConfirmation(Action confirmAction) {
    _dialogBoxShower.Create()
        .SetMessage(_uiFactory.T(UnsavedChangesConfirmationLocKey))
        .SetConfirmButton(confirmAction)
        .SetCancelButton(() => {})
        .Show();
  }

  void ShowPendingEditsNotification() {
    _dialogBoxShower.Create().SetMessage(_uiFactory.T(PendingEditsNotificationLocKey)).Show();
  }

  void SetActiveBuilding(AutomationBehavior activeBuilding) {
    Reset();
    _activeBuilding = activeBuilding;

    foreach (var action in activeBuilding.Actions) {
      var ruleRow = CreateScriptedRule();
      if (action is ScriptedAction scriptedAction && action.Condition is ScriptedCondition scriptedCondition) {
        ruleRow.Initialize(scriptedCondition.Expression, scriptedAction.Expression, scriptedAction.TemplateFamily);
      } else {
        ruleRow.Initialize(action);
      }
      ruleRow.SwitchToViewMode();
    }
  }

  void SaveRulesAndCloseDialog() {
    _activeBuilding.ClearAllRules();
    foreach (var rule in _ruleRows.Where(x => !x.IsDeleted)) {
      if (rule.LegacyAction != null) {
        var condition = rule.LegacyAction.Condition.CloneDefinition();
        var action = rule.LegacyAction.CloneDefinition();
        action.TemplateFamily = rule.TemplateFamily;
        _activeBuilding.AddRule(condition, action);
      } else {
        var condition = new ScriptedCondition();
        condition.SetExpression(rule.ConditionExpression);
        var action = new ScriptedAction();
        action.SetExpression(rule.ActionExpression);
        action.TemplateFamily = rule.TemplateFamily;
        _activeBuilding.AddRule(condition, action);
      }
    }
  }

  RuleRow CreateScriptedRule() {
    var ruleRow = new RuleRow(_editorProviders, _uiFactory, _activeBuilding);
    _ruleRows.Add(ruleRow);
    _ruleRowsContainer.Add(ruleRow.Root);
    return ruleRow;
  }

  #endregion
}
