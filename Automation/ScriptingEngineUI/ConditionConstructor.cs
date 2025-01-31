// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

class ConditionConstructor : BaseConstructor {
  
  const string ConditionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ConditionLabel";

  #region API

  public record ConditionDefinition {
    public DropdownItem<string> Argument { get; init; }
    public ScriptValue.TypeEnum ArgumentType { get; init; }
    public DropdownItem<string>[] ArgumentOptions { get; init; }
  }

  public override VisualElement Root { get; }
  public readonly ArgumentConstructor SignalSelector;
  public readonly SimpleDropdown<string> OperatorSelector;
  public readonly ArgumentConstructor ValueSelector;

  public ConditionConstructor(UiFactory uiFactory) : base(uiFactory) {
    SignalSelector = new ArgumentConstructor(uiFactory);
    SignalSelector.OnStringValueChanged += (_, _) => SetArgument(SignalSelector.Value);
    OperatorSelector = uiFactory.CreateValueDropdown<string>();
    ValueSelector = new ArgumentConstructor(uiFactory);

    Root = MakeRow(uiFactory.T(ConditionLabelLocKey),
                   SignalSelector.Root, OperatorSelector.DropdownElement, ValueSelector.Root);
  }

  public void SetDefinitions(IEnumerable<ConditionDefinition> lvalueDef) {
    _lvalueDefinitions = lvalueDef.ToArray();
    SignalSelector.SetDefinitions(_lvalueDefinitions.Select(x => x.Argument).ToArray());
    SetArgument(_lvalueDefinitions[0].Argument.Value);
  }

  public string Validate() {
    if (_selectedDefinition.ArgumentType == ScriptValue.TypeEnum.Number) {
      return ValueSelector.CheckInputForNumber();
    }
    if (_selectedDefinition.ArgumentType == ScriptValue.TypeEnum.String) {
      return ValueSelector.CheckInputForString();
    }
    return null;
  }

  public string GetScript() {
    var arg = SignalSelector.Value;
    var op = OperatorSelector.Value;
    var val = ValueSelector.Value;
    if (_selectedDefinition.ArgumentType == ScriptValue.TypeEnum.String) {
      val = "'" + val + "'";
    }
    return $"({op} (sig {arg}) {val})";
  }

  #endregion

  static readonly DropdownItem<string>[] StringOperators = [
      new() { Value = "eq", Text = "=" },
      new() { Value = "ne", Text = "<>" },
  ];

  static readonly DropdownItem<string>[] NumberOperators = [
      new() { Value = "eq", Text = "=" },
      new() { Value = "ne", Text = "<>" },
      new() { Value = "gt", Text = ">" },
      new() { Value = "lt", Text = "<" },
      new() { Value = "ge", Text = ">=" },
      new() { Value = "le", Text = "<=" },
  ];

  ConditionDefinition _selectedDefinition;
  ConditionDefinition[] _lvalueDefinitions;

  void SetArgument(string argument) {
    if (argument == null) {
      OperatorSelector.DropdownElement.ToggleDisplayStyle(false);
      ValueSelector.Root.ToggleDisplayStyle(false);
      return;
    }
    _selectedDefinition = _lvalueDefinitions.First(x => x.Argument.Value == argument);
    if (_selectedDefinition.ArgumentOptions == null) {
      OperatorSelector.DropdownElement.ToggleDisplayStyle(false);
      ValueSelector.Root.ToggleDisplayStyle(false);
    }
    OperatorSelector.Items =
        _selectedDefinition.ArgumentType == ScriptValue.TypeEnum.String ? StringOperators : NumberOperators;
    OperatorSelector.DropdownElement.ToggleDisplayStyle(true);
    ValueSelector.SetDefinitions(_selectedDefinition.ArgumentOptions);
    ValueSelector.Root.ToggleDisplayStyle(true);
  }
}
