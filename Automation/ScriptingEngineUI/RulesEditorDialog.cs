// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IgorZ.Automation.Actions;
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

  /// <summary>The rule rows of the dialog.</summary>
  public IReadOnlyList<RuleRow> RuleRows => _ruleRows;

  /// <summary>The scrolling view that holds the rules.</summary>
  public ScrollView ContentScrollView => _contentScrollView ??= Root.Q<ScrollView>();
  ScrollView _contentScrollView;

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
      if (provider.CreateRuleBtnLocKey == null) {
        continue;
      }
      var btn = UiFactory.LoadVisualElement<Button>(RulesEditorButtonTmplAsset);
      btn.text = UiFactory.T(provider.CreateRuleBtnLocKey);
      btn.clicked += () => provider.OnRuleRowBtnAction(CreateScriptedRule());
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

  /// <inheritdoc/>
  public override void Close() {
    base.Close();
    _ruleRows.Clear();
    _rulesUiHelper = null;
    _ruleRowsContainer = null;
  }

  /// <summary>Creates a new rule row at the end of the list.</summary>
  /// <remarks>The row is not initialized. The caller need to set values and trigger view mode change.</remarks>
  public RuleRow CreateScriptedRule() {
    return InsertScriptedRuleAt(_ruleRows.Count);
  }

  /// <summary>Creates a new rule row and inserts it at the specified position.</summary>
  /// <param name="index">The position to place the new rwo at.</param>
  public RuleRow InsertScriptedRuleAt(int index) {
    index = Math.Min(index, _ruleRows.Count);
    var ruleRow = RuleRow.CreateFor(this, _rulesUiHelper.AutomationBehavior);
    ruleRow.OnStateChanged += OnRuleStateChanged;
    _ruleRows.Insert(index, ruleRow);
    _ruleRowsContainer.Insert(index, ruleRow.Root);
    return ruleRow;
  }

  /// <summary>Swaps the two rows in rules order list.</summary>
  /// <remarks>
  /// The rules order does NOT determine the execution ordering! It's purely the representation thing.
  /// </remarks>
  public void SwapRows(int from, int to) {
    if (from < to) {
      (from, to) = (to, from);
    }
    var fromRuleRow = _ruleRows[from];
    _ruleRows.RemoveAt(from);
    _ruleRows.Insert(to, fromRuleRow);
    var fromRuleRowContainer = _ruleRowsContainer[from];
    _ruleRowsContainer.RemoveAt(from);
    _ruleRowsContainer.Insert(to, fromRuleRowContainer);
  }

  #endregion

  #region Implementation

  readonly ImmutableArray<IEditorButtonProvider> _editorProviders;

  bool RulesChanged => _ruleRows.Any(x => x.IsDeleted || x.IsModified);
  bool EditsPending => _ruleRows.Any(x => x.IsInEditMode);
  bool HasErrors => _ruleRows.Any(x => x.HasErrors);

  VisualElement _ruleRowsContainer;
  RulesUIHelper _rulesUiHelper;
  readonly List<RuleRow> _ruleRows = [];
  
  RulesEditorDialog(IEnumerable<IEditorButtonProvider> editorProviders) {
    _editorProviders = editorProviders.Where(x => x.RuleRowBtnLocKey != null).ToImmutableArray();
  }

  void Reset() {
    _ruleRowsContainer.Clear();
    _ruleRows.Clear();
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
