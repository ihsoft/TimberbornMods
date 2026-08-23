// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.AutomationSystemUI;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.Automation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RulesEditorDialog : AbstractDialog {

  const string RulesEditorDialogAsset = "IgorZ.Automation/RulesEditor";
  const string RulesEditorButtonTmplAsset = "IgorZ.Automation/RulesEditorButtonTmpl";

  const string PendingEditsNotificationLocKey = "IgorZ.Automation.Scripting.Editor.PendingEditsNotification";
  const string RulesWithErrorsLocKey = "IgorZ.Automation.Scripting.Editor.RulesWithErrorsNotification";
  const string GameAutomationConflictLocKey = "IgorZ.Automation.Scripting.Editor.GameAutomationConflictNotification";
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
    var gameAutomationConflictNotification = GetGameAutomationConflictNotification();
    if (gameAutomationConflictNotification != null) {
      return gameAutomationConflictNotification;
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

    _ruleRowsList = Root.Q2<ListView>("RuleRowsList");
    // ListView cannot serialize itemsSource, so UI Builder uses a separate authoring-time preview.
    Root.Q2<ScrollView>("RuleRowsPreview").RemoveFromHierarchy();
    _ruleRowsList.makeItem = MakeRuleRowContainer;
    _ruleRowsList.bindItem = BindRuleRow;
    _ruleRowsList.unbindItem = UnbindRuleRow;
    _ruleRowsList.itemsSource = _ruleRows;
    _ruleRowsList.itemIndexChanged += OnItemIndexChanged;
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
    if (_ruleRowsList != null) {
      _ruleRowsList.itemIndexChanged -= OnItemIndexChanged;
      _ruleRowsList.itemsSource = null;
    }
    _ruleRows.Clear();
    _rulesUiHelper = null;
    _ruleRowsList = null;
    base.Close();
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
    _ruleRowsList.RefreshItems();
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
    _ruleRowsList.RefreshItems();
    _ruleRowsList.ScrollToItem(to);
  }

  public enum GameAutomationRuleConflictState {
    None,
    BlocksGameAutomation,
    BlocksSave,
  }

  public GameAutomationRuleConflictState GetGameAutomationRuleConflictState(RuleRow ruleRow) {
    if (!EntityPanelSettings.PreventGameAutomationConflicts || !IsGameAutomationAvailable()) {
      return GameAutomationRuleConflictState.None;
    }
    var ruleNumber = _ruleRows.IndexOf(ruleRow) + 1;
    if (ruleNumber <= 0) {
      return GameAutomationRuleConflictState.None;
    }
    var rule = CreateRuleCandidate(ruleRow, ruleNumber);
    if (!_gameAutomationRuleSaveConflictDetector.IsConflictingRule(_rulesUiHelper.AutomationBehavior, rule)) {
      return GameAutomationRuleConflictState.None;
    }
    return IsControlledByGameAutomation()
        ? GameAutomationRuleConflictState.BlocksSave
        : GameAutomationRuleConflictState.BlocksGameAutomation;
  }

  #endregion

  #region Implementation

  readonly ImmutableArray<IEditorButtonProvider> _editorProviders;
  readonly GameAutomationRuleSaveConflictDetector _gameAutomationRuleSaveConflictDetector;

  bool RulesChanged => _ruleRows.Any(x => x.IsDeleted || x.IsModified);
  bool EditsPending => _ruleRows.Any(x => x.IsInEditMode);
  bool HasErrors => _ruleRows.Any(x => x.HasErrors);

  ListView _ruleRowsList;
  RulesUIHelper _rulesUiHelper;
  readonly List<RuleRow> _ruleRows = [];

  RulesEditorDialog(
      IEnumerable<IEditorButtonProvider> editorProviders,
      GameAutomationRuleSaveConflictDetector gameAutomationRuleSaveConflictDetector) {
    _editorProviders = editorProviders.Where(x => x.RuleRowBtnLocKey != null).ToImmutableArray();
    _gameAutomationRuleSaveConflictDetector = gameAutomationRuleSaveConflictDetector;
  }

  void Reset() {
    _ruleRows.Clear();
    _ruleRowsList.RefreshItems();
  }

  static VisualElement MakeRuleRowContainer() {
    var container = new VisualElement();
    container.AddToClassList("rules-editor-list__item");
    return container;
  }

  void BindRuleRow(VisualElement container, int index) {
    container.Clear();
    var ruleRoot = _ruleRows[index].Root;
    ruleRoot.RemoveFromHierarchy();
    container.Add(ruleRoot);
  }

  static void UnbindRuleRow(VisualElement container, int _) {
    container.Clear();
  }

  void OnItemIndexChanged(int _, int newIndex) {
    _ruleRowsList.ScrollToItem(newIndex);
  }

  void OnRuleStateChanged(object obj, EventArgs args) {
    var rowsChanged = false;
    for (var i = _ruleRows.Count - 1; i >= 0; i--) {
      var ruleRow = _ruleRows[i];
      if (ruleRow.IsDeleted && ruleRow.IsNew) {
        _ruleRows.RemoveAt(i);
        ruleRow.Root.RemoveFromHierarchy();
        rowsChanged = true;
      }
    }
    if (rowsChanged) {
      _ruleRowsList.RefreshItems();
    }
  }

  string GetGameAutomationConflictNotification() {
    if (!EntityPanelSettings.PreventGameAutomationConflicts || !IsGameAutomationAvailable()) {
      return null;
    }
    var conflictingRules = _gameAutomationRuleSaveConflictDetector.GetConflictingRuleNumbers(
        _rulesUiHelper.AutomationBehavior, IsControlledByGameAutomation(), GetRuleCandidates());
    return conflictingRules.Count == 0
        ? null
        : UiFactory.T(GameAutomationConflictLocKey, string.Join(", ", conflictingRules.Select(x => "#" + x)));
  }

  IEnumerable<GameAutomationRuleSaveConflictDetector.RuleCandidate> GetRuleCandidates() {
    return _ruleRows.Select((ruleRow, index) => CreateRuleCandidate(ruleRow, index + 1));
  }

  static GameAutomationRuleSaveConflictDetector.RuleCandidate CreateRuleCandidate(RuleRow ruleRow, int ruleNumber) {
    return new GameAutomationRuleSaveConflictDetector.RuleCandidate(
        RuleNumber: ruleNumber,
        IsDeleted: ruleRow.IsDeleted,
        IsEnabled: ruleRow.IsEnabled,
        ParsedAction: ruleRow.ParsedAction);
  }

  bool IsControlledByGameAutomation() {
    var automatable = GetGameAutomatable();
    return automatable && automatable.IsAutomated;
  }

  bool IsGameAutomationAvailable() {
    return GetGameAutomatable();
  }

  Automatable GetGameAutomatable() {
    return _rulesUiHelper.AutomationBehavior.GetComponent<Automatable>();
  }

  #endregion
}
