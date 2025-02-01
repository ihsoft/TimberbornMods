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
using TimberApi.UIPresets.ScrollViews;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using UnityEngine.UIElements;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngineUI;

class RulesEditorDialog : IPostLoadableSingleton {

  const string AddRuleFromScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.AddRuleFromScriptBtn";
  const string AddRuleViaConstructorBtnLocKey = "IgorZ.Automation.Scripting.Editor.AddRuleViaConstructorBtn";
  const string EditAsScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.EditAsScriptBtn";
  const string EditInConstructorBtnLocKey = "IgorZ.Automation.Scripting.Editor.EditInConstructorBtn";
  const string SaveRulesBtnLocKey = "IgorZ.Automation.Scripting.Editor.SaveRules";
  const string DiscardChangesBtnLocKey = "IgorZ.Automation.Scripting.Editor.DiscardChanges";

  const float ContentWidthRatio = 0.6f;
  const float ContentHeightRatio = 0.6f;

  #region API

  public void Show(AutomationBehavior behavior, Action onClosed) {
    var builder = _dialogBoxShower.Create()
        .SetMaxWidth(_contentMaxWidth)
        .SetConfirmButton(() => SaveRulesAndCloseDialog(onClosed), _uiFactory.T(SaveRulesBtnLocKey))
        .SetCancelButton(() => {}, _uiFactory.T(DiscardChangesBtnLocKey))
        .AddContent(CreateContent());
    builder._root.Q<VisualElement>("Box").style.width = _contentMaxWidth;

    _ruleRowsContainer = UiFactory.FindElementUpstream<VisualElement>(builder._root, "RuleRowsContainer");
    _confirmButton = UiFactory.FindElementUpstream<Button>(builder._root, "ConfirmButton");

    SetActiveBuilding(behavior);
    builder.Show();
  }

  #endregion

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {}

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  readonly DialogBoxShower _dialogBoxShower;
  readonly ScriptEditorProvider _scriptEditorProvider;
  readonly ConstructorEditorProvider _constructorEditorProvider;

  readonly int _contentMaxWidth;
  readonly int _contentMaxHeight;

  readonly List<RuleRow> _ruleRows = []; 
  readonly (string, IEditorProvider)[] _editorProviders;

  VisualElement _ruleRowsContainer;
  AutomationBehavior _activeBuilding;
  Button _confirmButton;

  RulesEditorDialog(UiFactory uiFactory, DialogBoxShower dialogBoxShower,
                    ScriptEditorProvider scriptEditorProvider,
                    ConstructorEditorProvider constructorEditorProvider) {
    _uiFactory = uiFactory;
    _dialogBoxShower = dialogBoxShower;
    _scriptEditorProvider = scriptEditorProvider;
    _constructorEditorProvider = constructorEditorProvider;
    _contentMaxWidth = Mathf.RoundToInt(Screen.width * ContentWidthRatio);
    _contentMaxHeight = Mathf.RoundToInt(Screen.height * ContentHeightRatio);
    _editorProviders = [
        (EditAsScriptBtnLocKey, scriptEditorProvider),
        (EditInConstructorBtnLocKey, constructorEditorProvider),
    ];
  }

  VisualElement CreateContent() {
    var root = new VisualElement();
    root.style.minHeight = _contentMaxHeight;
    root.Add(new VisualElement {
        name = "RuleRowsContainer",
    });
    var buttons = new VisualElement();
    buttons.style.flexDirection = FlexDirection.Row;
    buttons.style.marginTop = 10;
    root.Add(buttons);

    var addRuleFromScriptBtn = _uiFactory.CreateButton(
        AddRuleFromScriptBtnLocKey, _ => _scriptEditorProvider.MakeForRule(CreateScriptedRule()));
    addRuleFromScriptBtn.style.marginRight = 5;
    buttons.Add(addRuleFromScriptBtn);
    var addRuleViaConstructorBtn = _uiFactory.CreateButton(
        AddRuleViaConstructorBtnLocKey, _ => _constructorEditorProvider.MakeForRule(CreateScriptedRule()));
    addRuleViaConstructorBtn.style.marginRight = 5;
    buttons.Add(addRuleViaConstructorBtn);

    var scrollView = _uiFactory.UiBuilder.Create<DefaultScrollView>()
        .SetMaxHeight(_contentMaxHeight)
        .BuildAndInitialize();
    scrollView.Add(root);

    return scrollView;
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

  void SaveRulesAndCloseDialog(Action onClosed) {
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
    onClosed?.Invoke();
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
      if (_ruleRows[i].IsDeleted) {
        _ruleRows[i].Root.RemoveFromHierarchy();
        _ruleRows.RemoveAt(i);
      }
    }
    _confirmButton.SetEnabled(_ruleRows.All(r => !r.IsInEditMode));
  }

  #endregion
}
