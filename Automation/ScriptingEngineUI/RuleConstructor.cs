// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RuleConstructor : BaseConstructor {

  const string RuleRowButtonTemplate = "IgorZ.Automation/RuleRowButtonTmpl";

  const string AndOperatorLocKey = "IgorZ.Automation.Scripting.Expressions.AndOperator";
  const string OrOperatorLocKey = "IgorZ.Automation.Scripting.Expressions.OrOperator";
  const string AndOperatorValue = "and";
  const string OrOperatorValue = "or";
  const string RemoveConditionButtonText = "×";

  #region API

  public readonly record struct ConditionEntry(LogicalOperator.OpType JoinOperator, ComparisonOperator Comparison);

  public override VisualElement Root { get; }
  public readonly ActionConstructor ActionConstructor;

  public void SetConditionDefinitions(IEnumerable<ConditionConstructor.ConditionDefinition> definitions) {
    _conditionDefinitions = definitions.ToArray();
    AddCondition(LogicalOperator.OpType.And);
  }

  public string Validate() {
    return _conditions.Select(x => x.Constructor.Validate()).FirstOrDefault(x => x != null)
        ?? ActionConstructor.Validate();
  }

  public string GetConditionLispScript() {
    var groups = new List<List<string>> { new() };
    foreach (var condition in _conditions) {
      if (condition != _conditions[0] && condition.JoinOperator == LogicalOperator.OpType.Or) {
        groups.Add([]);
      }
      groups[^1].Add(condition.Constructor.GetLispScript());
    }
    var groupScripts = groups.Select(group => group.Count == 1 ? group[0] : $"(and {string.Join(" ", group)})");
    return groups.Count == 1 ? groupScripts.Single() : $"(or {string.Join(" ", groupScripts)})";
  }

  public void SetConditions(IReadOnlyList<ConditionEntry> conditions) {
    foreach (var condition in _conditions.ToArray()) {
      RemoveCondition(condition);
    }
    foreach (var condition in conditions) {
      var row = AddCondition(condition.JoinOperator);
      row.Constructor.SetComparison(condition.Comparison);
    }
  }

  #endregion

  #region Implementation

  sealed class ConditionRow {
    public readonly VisualElement Root;
    public readonly ConditionConstructor Constructor;
    public LogicalOperator.OpType JoinOperator;
    public readonly ResizableDropdownElement JoinSelector;
    public readonly Button RemoveButton;

    public ConditionRow(
        VisualElement root, ConditionConstructor constructor, LogicalOperator.OpType joinOperator,
        ResizableDropdownElement joinSelector, Button removeButton) {
      Root = root;
      Constructor = constructor;
      JoinOperator = joinOperator;
      JoinSelector = joinSelector;
      RemoveButton = removeButton;
    }
  }

  readonly UiFactory _uiFactory;
  readonly VisualElement _conditionsRoot;
  readonly List<ConditionRow> _conditions = [];
  ConditionConstructor.ConditionDefinition[] _conditionDefinitions;

  public RuleConstructor(UiFactory uiFactory, VisualElement root) : base(uiFactory) {
    _uiFactory = uiFactory;
    Root = root;
    _conditionsRoot = Root.Q2<VisualElement>("ConditionsRoot");
    ActionConstructor = new ActionConstructor(uiFactory);
    Root.Q2<VisualElement>("ActionRoot").Add(ActionConstructor.Root);
    Root.Q2<Button>("AddAndConditionBtn").clicked += () => AddCondition(LogicalOperator.OpType.And);
    Root.Q2<Button>("AddOrConditionBtn").clicked += () => AddCondition(LogicalOperator.OpType.Or);
  }

  ConditionRow AddCondition(LogicalOperator.OpType joinOperator) {
    var conditionConstructor = new ConditionConstructor(_uiFactory);
    conditionConstructor.SetDefinitions(_conditionDefinitions);

    ConditionRow conditionRow = null;
    ResizableDropdownElement joinSelector = null;
    joinSelector = _uiFactory.CreateSimpleDropdown(value => {
      if (conditionRow != null) {
        conditionRow.JoinOperator = value == AndOperatorValue
            ? LogicalOperator.OpType.And
            : LogicalOperator.OpType.Or;
      }
    });
    joinSelector.Items = [
        new DropdownItem { Value = AndOperatorValue, Text = _uiFactory.T(AndOperatorLocKey).ToUpperInvariant() },
        new DropdownItem { Value = OrOperatorValue, Text = _uiFactory.T(OrOperatorLocKey).ToUpperInvariant() },
    ];
    joinSelector.SelectedValue = joinOperator == LogicalOperator.OpType.And ? AndOperatorValue : OrOperatorValue;

    var removeButton = CreateButton(RemoveConditionButtonText, _ => RemoveCondition(conditionRow), localize: false);
    var rowRoot = MakeRow(removeButton, joinSelector, conditionConstructor.Root);
    conditionRow = new ConditionRow(rowRoot, conditionConstructor, joinOperator, joinSelector, removeButton);
    _conditions.Add(conditionRow);
    _conditionsRoot.Add(rowRoot);
    UpdateRowControls();
    return conditionRow;
  }

  Button CreateButton(string text, Action<Button> onClick, bool localize = true) {
    var button = _uiFactory.LoadVisualElement<Button>(RuleRowButtonTemplate);
    button.text = localize ? _uiFactory.T(text) : text;
    button.clicked += () => onClick(button);
    return button;
  }

  void RemoveCondition(ConditionRow row) {
    _conditions.Remove(row);
    row.Root.RemoveFromHierarchy();
    UpdateRowControls();
  }

  void UpdateRowControls() {
    for (var i = 0; i < _conditions.Count; i++) {
      _conditions[i].Constructor.ConditionLabel.ToggleDisplayStyle(i == 0);
      _conditions[i].JoinSelector.ToggleDisplayStyle(i > 0);
      _conditions[i].RemoveButton.ToggleDisplayStyle(_conditions.Count > 1);
    }
  }

  #endregion
}
