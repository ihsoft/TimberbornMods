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
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RulesEditorDialog : IPanelController {

  #region IPanelController implementation

  /// <inheritdoc/>
  public VisualElement GetPanel() {
    return _root;
  }

  /// <inheritdoc/>
  public bool OnUIConfirmed() {
    SaveRulesAndCloseDialog();
    Close();
    _onClosed?.Invoke();
    return false;
  }

  /// <inheritdoc/>
  public void OnUICancelled() {
    if (!_closingByButton && _hasUnsavedChanges) {
      //FIXME: show a confirmation dialog
      DebugEx.Warning("*** Has unsaved changes!");
    }
    Close();
  }
  Action _onClosed;
  bool _closingByButton;

  #endregion

  #region API

  public void Show(AutomationBehavior behavior, Action onClosed) {
    _root = _uiFactory.LoadVisualTreeAsset("IgorZ.Automation/RulesEditor");
    _ruleRowsContainer = _root.Q<VisualElement>("RuleRowsContainer");
    _confirmButton = _root.Q<Button>("ConfirmButton");
    _confirmButton.clicked += () => OnUIConfirmed();
    _root.Q<Button>("CancelButton").clicked += () => {
      _closingByButton = true;
      OnUICancelled();
      _closingByButton = false;
    };
    //FIXME: implement
    _root.Q<Button>("MoreInfoButton").ToggleDisplayStyle(false);

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

  VisualElement _root;

  readonly List<RuleRow> _ruleRows = []; 
  readonly IEditorProvider[] _editorProviders;

  bool _hasUnsavedChanges;

  VisualElement _ruleRowsContainer;
  AutomationBehavior _activeBuilding;
  Button _confirmButton;

  RulesEditorDialog(UiFactory uiFactory, PanelStack panelStack,
                    ScriptEditorProvider scriptEditorProvider,
                    ConstructorEditorProvider constructorEditorProvider) {
    _uiFactory = uiFactory;
    _panelStack = panelStack;
    _editorProviders = [scriptEditorProvider, constructorEditorProvider];
  }

  void SetActiveBuilding(AutomationBehavior activeBuilding) {
    _activeBuilding = activeBuilding;
    _ruleRows.Clear();
    _ruleRowsContainer.Clear();

    foreach (var action in activeBuilding.Actions) {
      var legacyAction = action;
      var scriptedAction = action as ScriptedAction;
      var scriptedCondition = action.Condition as ScriptedCondition;
      if (scriptedAction != null && scriptedCondition != null) {
        legacyAction = null;
      }
      var ruleRow = CreateScriptedRule(legacyAction);
      if (scriptedCondition != null && scriptedAction != null) {
        ruleRow.ConditionExpression = scriptedCondition.Expression;
        ruleRow.ActionExpression = scriptedAction.Expression;
      }
      ruleRow.SwitchToViewMode();
    }
  }

  void SaveRulesAndCloseDialog() {
    _activeBuilding.ClearAllRules();
    foreach (var rule in _ruleRows) {
      if (rule.LegacyAction != null) {
        _activeBuilding.AddRule(rule.LegacyAction.Condition, rule.LegacyAction);
      } else {
        var condition = new ScriptedCondition();
        condition.SetExpression(rule.ConditionExpression);
        var action = new ScriptedAction();
        action.SetExpression(rule.ActionExpression);
        _activeBuilding.AddRule(condition, action);
      }
    }
  }

  RuleRow CreateScriptedRule(IAutomationAction legacyAction = null) {
    var ruleRow = new RuleRow(_editorProviders, _uiFactory, _activeBuilding) { LegacyAction = legacyAction };
    ruleRow.OnModeChanged += UpdateButtonsState;
    _ruleRows.Add(ruleRow);
    _ruleRowsContainer.Add(ruleRow.Root);
    return ruleRow;
  }

  void UpdateButtonsState(object obj, EventArgs args) {
    for (var i = _ruleRows.Count - 1; i >= 0; i--) {
      _hasUnsavedChanges |= _ruleRows[i].IsInEditMode;
      if (_ruleRows[i].IsDeleted) {
        _ruleRows[i].Root.RemoveFromHierarchy();
        _ruleRows.RemoveAt(i);
        _hasUnsavedChanges = true;
      }
    }
    _confirmButton.SetEnabled(_ruleRows.All(r => !r.IsInEditMode));
  }

  #endregion
}
