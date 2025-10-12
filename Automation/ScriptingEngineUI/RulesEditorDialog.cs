// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.AutomationSystemUI;
using IgorZ.Automation.Conditions;
using IgorZ.TimberDev.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RulesEditorDialog : AbstractDialog {

  const string RulesEditorDialogAsset = "IgorZ.Automation/RulesEditor";
  const string RulesEditorButtonTmplAsset = "IgorZ.Automation/RulesEditorButtonTmpl";

  const string PendingEditsNotificationLocKey = "IgorZ.Automation.Scripting.Editor.PendingEditsNotification";
  const string RulesWithErrorsLocKey = "IgorZ.Automation.Scripting.Editor.RulesWithErrorsNotification";
  const string ReadMoreLinkLocKey = "IgorZ.Automation.Scripting.Editor.ReadMoreLink";

  #region AbstractDialog implementation

  /// <inheritdoc/>
  protected override string DialogResourceName => RulesEditorDialogAsset;

  /// <inheritdoc/>
  protected override string VerifyInput() {
    if (EditsPending) {
      return UiFactory.T(PendingEditsNotificationLocKey);
    }
    if (HasErrors && !Keyboard.current.ctrlKey.isPressed) {
      return UiFactory.T(RulesWithErrorsLocKey);
    }
    return null;
  }

  /// <inheritdoc/>
  protected override void ApplyInput() {
    _rulesUiHelper.ClearRulesOnBuilding();
    foreach (var rule in _ruleRows.Where(x => !x.IsDeleted)) {
      _rulesUiHelper.AutomationBehavior.AddRule(rule.GetCondition(), rule.GetAction());
    }
  }

  /// <inheritdoc/>
  protected override bool CheckHasChanges() {
    return RulesChanged || EditsPending;
  }

  #endregion

  #region API

  public RulesEditorDialog WithUiHelper(RulesUIHelper rulesUiHelper) {
    _rulesUiHelper = rulesUiHelper;
    return this;
  }

  /// <inheritdoc/>
  public override void Show() {
    base.Show();

    Root.Q2<Button>("MoreInfoButton").clicked += () => Application.OpenURL(UiFactory.T(ReadMoreLinkLocKey));

    var buttons = Root.Q2<VisualElement>("Buttons");
    buttons.Clear();
    foreach (var provider in _editorProviders) {
      var btn = UiFactory.LoadVisualElement<Button>(RulesEditorButtonTmplAsset);
      btn.text = UiFactory.T(provider.CreateRuleLocKey);
      btn.clicked += () => provider.MakeForRule(CreateScriptedRule());
      buttons.Add(btn);
    }

    _ruleRowsContainer = Root.Q2<VisualElement>("RuleRowsContainer");
    Reset();
    foreach (var action in _rulesUiHelper.BuildingRules) {
      var ruleRow = CreateScriptedRule();
      if (action is ScriptedAction scriptedAction && action.Condition is ScriptedCondition scriptedCondition) {
        ruleRow.Initialize(scriptedCondition, scriptedAction);
      } else {
        ruleRow.Initialize(action);
      }
      ruleRow.SwitchToViewMode();
    }
  }

  public override void Close() {
    base.Close();
    _ruleRows.Clear();
    _rulesUiHelper = null;
    _ruleRowsContainer = null;
  }

  #endregion

  #region Implementation

  IEditorProvider[] _editorProviders;

  bool RulesChanged => _ruleRows.Any(x => x.IsDeleted || x.IsModified);
  bool EditsPending => _ruleRows.Any(x => x.IsInEditMode);
  bool HasErrors => _ruleRows.Any(x => x.HasErrors);

  VisualElement _ruleRowsContainer;
  RulesUIHelper _rulesUiHelper;
  readonly List<RuleRow> _ruleRows = []; 
  
  /// <summary>Public for the inject to work properly.</summary>
  [Inject]
  public void InjectDependencies(
      ScriptEditorProvider scriptEditorProvider, ConstructorEditorProvider constructorEditorProvider) {
    _editorProviders = [scriptEditorProvider, constructorEditorProvider];
  }

  void Reset() {
    _ruleRowsContainer.Clear();
    _ruleRows.Clear();
  }

  RuleRow CreateScriptedRule() {
    var ruleRow = new RuleRow(_editorProviders, UiFactory, _rulesUiHelper.AutomationBehavior);
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
