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

  public ConditionConstructor(UiFactory uiFactory) : base(uiFactory) {
    _operatorSelector = new ArgumentConstructor(uiFactory);
    _operatorSelector.OnStringValueChanged += (_, _) => SetArgument(_operatorSelector.Value);
    _operandSelector = uiFactory.CreateValueDropdown<string>((_, _) => {});
    _valueSelector = new ArgumentConstructor(uiFactory);

    Root = MakeRow(uiFactory.T(ConditionLabelLocKey),
                   _operatorSelector.Root, _operandSelector.DropdownElement, _valueSelector.Root);
  }

  public void SetDefinitions(IEnumerable<ConditionDefinition> lvalueDef) {
    _lvalueDefinitions = lvalueDef.ToArray();
    _operatorSelector.SetDefinitions(_lvalueDefinitions.Select(x => x.Argument).ToArray());
    SetArgument(_lvalueDefinitions[0].Argument.Value);
  }

  public string Validate() {
    //var def = _lvalueDefinitions.First(x => x.Argument.Value == _operatorSelector.Value);
    if (_selectedDefinition.ArgumentType == ScriptValue.TypeEnum.Number) {
      return _valueSelector.CheckInputForNumber();
    }
    if (_selectedDefinition.ArgumentType == ScriptValue.TypeEnum.String) {
      return _valueSelector.CheckInputForString();
    }
    return null;
  }

  public string GetScript() {
    var arg = _operatorSelector.Value;
    var op = _operandSelector.Value;
    var val = _valueSelector.Value;
    //var def = _lvalueDefinitions.First(x => x.Argument.Value == arg);
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

  readonly ArgumentConstructor _operatorSelector;
  readonly SimpleDropdown<string> _operandSelector;
  readonly ArgumentConstructor _valueSelector;

  ConditionDefinition _selectedDefinition;
  ConditionDefinition[] _lvalueDefinitions;

  void SetArgument(string argument) {
    if (argument == null) {
      _operandSelector.DropdownElement.ToggleDisplayStyle(false);
      _valueSelector.Root.ToggleDisplayStyle(false);
      return;
    }
    _selectedDefinition = _lvalueDefinitions.First(x => x.Argument.Value == argument);
    if (_selectedDefinition.ArgumentOptions == null) {
      _operandSelector.DropdownElement.ToggleDisplayStyle(false);
      _valueSelector.Root.ToggleDisplayStyle(false);
    }
    _operandSelector.Items =
        _selectedDefinition.ArgumentType == ScriptValue.TypeEnum.String ? StringOperators : NumberOperators;
    _operandSelector.DropdownElement.ToggleDisplayStyle(true);
    _valueSelector.SetDefinitions(_selectedDefinition.ArgumentOptions);
    _valueSelector.Root.ToggleDisplayStyle(true);
  }
}
