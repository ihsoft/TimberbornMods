﻿// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.TimberDev.UI;
using TimberApi.DependencyContainerSystem;
using Timberborn.CoreUI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RulesEditorDialog : IPanelController {

  const string PendingEditsNotificationLocKey = "IgorZ.Automation.Scripting.Editor.PendingEditsNotification";
  const string UnsavedChangesConfirmationLocKey = "IgorZ.Automation.Scripting.Editor.UnsavedChangesConfirmation";
  const string RulesWithErrorsLocKey = "IgorZ.Automation.Scripting.Editor.RulesWithErrorsNotification";
  const string ReadMoreLinkLocKey = "IgorZ.Automation.Scripting.Editor.ReadMoreLink";

  #region IPanelController implementation

  /// <inheritdoc/>
  public VisualElement GetPanel() {
    return _root;
  }

  /// <inheritdoc/>
  public bool OnUIConfirmed() {
    if (EditsPending) {
      _dialogBoxShower.Create().SetMessage(_uiFactory.T(PendingEditsNotificationLocKey)).Show();
      return true;
    }
    if (HasErrors && !Keyboard.current.ctrlKey.isPressed) {
      _dialogBoxShower.Create().SetMessage(_uiFactory.T(RulesWithErrorsLocKey)).Show();
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

    _root.Q<Button>("ImportRulesButton").clicked += () => {
      var dlg = DependencyContainer.GetInstance<ImportRulesDialog>();
      dlg.Show(_activeBuilding, rules => OnImportComplete(rules, dlg.DeleteExistingRules));
    };
    _root.Q<Button>("ExportRulesButton").clicked += () => {
      var dlg = DependencyContainer.GetInstance<ExportRulesDialog>();
      var actions= new List<IAutomationAction>();
      foreach (var rule in _ruleRows.Where(x => !x.IsDeleted)) {
        var exportAction = rule.GetAction().CloneDefinition();
        exportAction.Condition = rule.GetCondition().CloneDefinition();
        actions.Add(exportAction);
      }
      dlg.Show(actions);
    };

    SetActiveBuilding(behavior);

    _onClosed = onClosed;
    _panelStack.PushDialog(this);
  }

  void OnImportComplete(IList<IAutomationAction> rules, bool clearExisting) {
    if (clearExisting) {
      Reset();
    }
    foreach (var rule in rules) {
      var ruleRow = CreateScriptedRule();
      if (rule is ScriptedAction scriptedAction && rule.Condition is ScriptedCondition scriptedCondition) {
        ruleRow.Initialize(scriptedCondition, scriptedAction);
      }
      ruleRow.SwitchToViewMode();
    }
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
  bool HasErrors => _ruleRows.Any(x => x.HasErrors);

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

  void SetActiveBuilding(AutomationBehavior activeBuilding) {
    Reset();
    _activeBuilding = activeBuilding;

    foreach (var action in activeBuilding.Actions) {
      var ruleRow = CreateScriptedRule();
      if (action is ScriptedAction scriptedAction && action.Condition is ScriptedCondition scriptedCondition) {
        ruleRow.Initialize(scriptedCondition, scriptedAction);
      } else {
        ruleRow.Initialize(action);
      }
      ruleRow.SwitchToViewMode();
    }
  }

  void SaveRulesAndCloseDialog() {
    _activeBuilding.ClearAllRules();
    foreach (var rule in _ruleRows.Where(x => !x.IsDeleted)) {
      _activeBuilding.AddRule(rule.GetCondition(), rule.GetAction());
    }
  }

  RuleRow CreateScriptedRule() {
    var ruleRow = new RuleRow(_editorProviders, _uiFactory, _activeBuilding);
    ruleRow.OnStateChanged += OnRuleStateChanged;
    _ruleRows.Add(ruleRow);
    _ruleRowsContainer.Add(ruleRow.Root);
    return ruleRow;
  }

  void OnRuleStateChanged(object obj, EventArgs args) {
    for (var i = _ruleRows.Count - 1; i >= 0; i--) {
      var ruleRow = _ruleRows[i];
      if (ruleRow.IsDeleted && ruleRow.IsNew) {
        _ruleRows.RemoveAt(i);
        ruleRow.Root.RemoveFromHierarchy();
      }
    }
  }

  #endregion
}
