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

  #region API

  public record ConditionDefinition {
    public DropdownItem Name { get; init; }
    public ArgumentDefinition Argument { get; init; }
  }

  public override VisualElement Root { get; }
  public readonly ResizableDropdownElement SignalSelector;
  public readonly ResizableDropdownElement OperatorSelector;
  public readonly ArgumentConstructor ValueSelector;

  public void SetDefinitions(IEnumerable<ConditionDefinition> lvalueDef) {
    _lvalueDefinitions = lvalueDef.ToArray();
    SignalSelector.Items = _lvalueDefinitions.Select(x => x.Name).ToArray();
    SetArgument(_lvalueDefinitions[0].Name.Value);
  }

  public string Validate() => ValueSelector.Validate();

  public string GetLispScript() {
    var arg = SignalSelector.SelectedValue;
    var op = OperatorSelector.SelectedValue;
    var val = ValueSelector.GetScriptValue();
    return $"({op} (sig {arg}) {val})";
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

  public ConditionConstructor(UiFactory uiFactory) : base(uiFactory) {
    SignalSelector = uiFactory.CreateSimpleDropdown(SetArgument);
    OperatorSelector = uiFactory.CreateSimpleDropdown();
    ValueSelector = new ArgumentConstructor(uiFactory);

    Root = MakeRow(uiFactory.T(ConditionLabelLocKey), SignalSelector, OperatorSelector, ValueSelector.Root);
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

  #endregion
}
