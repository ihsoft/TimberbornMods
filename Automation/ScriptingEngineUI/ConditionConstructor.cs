// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

class ConditionConstructor : BaseConstructor {

  const string ConditionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ConditionLabel";
  const int MinimumGroupSize = 3;

  #region API

  public record ConditionDefinition {
    public DropdownItem Name { get; init; }
    public string DisplayNameLocKey { get; init; }
    public string DisplayNameArgument { get; init; }
    public ArgumentDefinition Argument { get; init; }
  }

  public override VisualElement Root { get; }
  public readonly ResizableDropdownElement SignalSelector;
  public readonly ResizableDropdownElement GroupedSignalSelector;
  public readonly ResizableDropdownElement OperatorSelector;
  public readonly ArgumentConstructor ValueSelector;
  public readonly Label ConditionLabel;

  public void SetDefinitions(IEnumerable<ConditionDefinition> lvalueDef) {
    _lvalueDefinitions = lvalueDef.ToArray();
    _signalGroups = MakeSignalGroups(_lvalueDefinitions);
    SignalSelector.Items = MakePrimaryItems(_lvalueDefinitions, _signalGroups);
  }

  public string Validate() => ValueSelector.Validate();

  public string GetLispScript() {
    var arg = _selectedDefinition.Name.Value;
    var op = OperatorSelector.SelectedValue;
    var val = ValueSelector.GetScriptValue();
    return $"({op} (sig {arg}) {val})";
  }

  public void SetComparison(ComparisonOperator comparisonOperator) {
    SelectSignal((comparisonOperator.Left as SignalOperator)!.SignalName);
    OperatorSelector.SelectedValue = LispSyntaxParser.ComparisonOperators[comparisonOperator.OperatorType];
    if (comparisonOperator.Right is not ConstantValueExpr constantValue) {
      throw new InvalidOperationException("Constant value is expected");
    }
    ValueSelector.SetScriptValue(constantValue.ValueFn());
  }

  #endregion

  #region Implementation

  static readonly DropdownItem[] StringOperators = [
      new() { Value = LispSyntaxParser.ComparisonOperators[ComparisonOperator.OpType.Equal], Text = "=" },
      new() { Value = LispSyntaxParser.ComparisonOperators[ComparisonOperator.OpType.NotEqual], Text = "\u2260" },
  ];

  static readonly DropdownItem[] NumberOperators = [
      new() { Value = LispSyntaxParser.ComparisonOperators[ComparisonOperator.OpType.Equal], Text = "=" },
      new() { Value = LispSyntaxParser.ComparisonOperators[ComparisonOperator.OpType.NotEqual], Text = "\u2260" },
      new() { Value = LispSyntaxParser.ComparisonOperators[ComparisonOperator.OpType.GreaterThan], Text = ">" },
      new() { Value = LispSyntaxParser.ComparisonOperators[ComparisonOperator.OpType.LessThan], Text = "<" },
      new() { Value = LispSyntaxParser.ComparisonOperators[ComparisonOperator.OpType.GreaterThanOrEqual], Text = "\u2265" },
      new() { Value = LispSyntaxParser.ComparisonOperators[ComparisonOperator.OpType.LessThanOrEqual], Text = "\u2264" },
  ];

  ConditionDefinition _selectedDefinition;
  ConditionDefinition[] _lvalueDefinitions;
  SignalGroup[] _signalGroups;

  public ConditionConstructor(UiFactory uiFactory) : base(uiFactory) {
    SignalSelector = uiFactory.CreateSimpleDropdown(SetPrimarySelection);
    GroupedSignalSelector = uiFactory.CreateSimpleDropdown(SetArgument);
    GroupedSignalSelector.ToggleDisplayStyle(false);
    OperatorSelector = uiFactory.CreateSimpleDropdown();
    ValueSelector = new ArgumentConstructor(uiFactory);
    ConditionLabel = uiFactory.CreateLabel(classes: [UiFactory.GameTextBigClass]);
    ConditionLabel.text = uiFactory.T(ConditionLabelLocKey);
    ConditionLabel.style.marginRight = 5;

    Root = MakeRow(ConditionLabel, SignalSelector, GroupedSignalSelector, OperatorSelector, ValueSelector.Root);
  }

  void SetPrimarySelection(string value) {
    var group = _signalGroups?.FirstOrDefault(x => x.Id == value);
    if (group != null) {
      GroupedSignalSelector.Items = group.Definitions.Select(x => new DropdownItem {
          Value = x.Name.Value,
          Text = x.DisplayNameArgument,
          Icon = x.Name.Icon,
      }).ToArray();
      GroupedSignalSelector.ToggleDisplayStyle(true);
      return;
    }
    GroupedSignalSelector.ToggleDisplayStyle(false);
    SetArgument(value);
  }

  void SelectSignal(string signalName) {
    var group = _signalGroups.FirstOrDefault(x => x.Definitions.Any(definition => definition.Name.Value == signalName));
    if (group == null) {
      SignalSelector.SelectedValue = signalName;
      return;
    }
    SignalSelector.SelectedValue = group.Id;
    GroupedSignalSelector.SelectedValue = signalName;
  }

  void SetArgument(string argument) {
    if (argument == null) {
      OperatorSelector.ToggleDisplayStyle(false);
      ValueSelector.Root.ToggleDisplayStyle(false);
      return;
    }
    _selectedDefinition = _lvalueDefinitions.First(x => x.Name.Value == argument);
    if (_selectedDefinition.Argument.ValueOptions == null) {
      OperatorSelector.ToggleDisplayStyle(false);
      ValueSelector.Root.ToggleDisplayStyle(false);
    }
    OperatorSelector.Items = _selectedDefinition.Argument.ValueType switch {
        ScriptValue.TypeEnum.String => StringOperators,
        ScriptValue.TypeEnum.Number => NumberOperators,
        ScriptValue.TypeEnum.Unset => throw new InvalidOperationException("Value type must be set"),
    };
    OperatorSelector.ToggleDisplayStyle(true);
    ValueSelector.SetDefinition(_selectedDefinition.Argument);
    ValueSelector.Root.ToggleDisplayStyle(true);
  }

  SignalGroup[] MakeSignalGroups(IEnumerable<ConditionDefinition> definitions) {
    return ParameterizedItemsGrouper.MakeGroups(
            definitions,
            definition => definition.DisplayNameArgument != null ? definition.DisplayNameLocKey : null,
            MinimumGroupSize)
        .Select(group => new SignalGroup(
            $"\0signal-group:{group.Key}", MakeGroupName(group.Items[0]), group.Items))
        .ToArray();
  }

  string MakeGroupName(ConditionDefinition definition) {
    return UIFactory.T(definition.DisplayNameLocKey, "\u2026");
  }

  static DropdownItem[] MakePrimaryItems(
      IEnumerable<ConditionDefinition> definitions, IReadOnlyCollection<SignalGroup> groups) {
    var result = new List<DropdownItem>();
    var addedGroups = new HashSet<string>();
    foreach (var definition in definitions) {
      var group = groups.FirstOrDefault(x => x.Definitions.Contains(definition));
      if (group == null) {
        result.Add(definition.Name);
      } else if (addedGroups.Add(group.Id)) {
        result.Add(new DropdownItem { Value = group.Id, Text = group.Name });
      }
    }
    return result.ToArray();
  }

  sealed record SignalGroup(string Id, string Name, ConditionDefinition[] Definitions);

  #endregion
}
