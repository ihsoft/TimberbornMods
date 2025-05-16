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
    public DropdownItem<string> Name { get; init; }
    public ArgumentDefinition Argument { get; init; }
  }

  public override VisualElement Root { get; }
  public readonly ArgumentConstructor SignalSelector;
  public readonly ResizableDropdownElement OperatorSelector;
  public readonly ArgumentConstructor ValueSelector;

  public void SetDefinitions(IEnumerable<ConditionDefinition> lvalueDef) {
    _lvalueDefinitions = lvalueDef.ToArray();
    SignalSelector.SetOptions(_lvalueDefinitions.Select(x => x.Name).ToArray());
    SetArgument(_lvalueDefinitions[0].Name.Value);
  }

  public string Validate() => ValueSelector.Validate();

  public string GetScript() {
    var arg = SignalSelector.Value;
    var op = OperatorSelector.SelectedValue;
    var val = ValueSelector.GetScriptValue();
    return $"({op} (sig {arg}) {val})";
  }

  #endregion

  #region Implementation

  static readonly DropdownItem<string>[] StringOperators = [
      new() { Value = "eq", Text = "=" },
      new() { Value = "ne", Text = "\u2260" },
  ];

  static readonly DropdownItem<string>[] NumberOperators = [
      new() { Value = "eq", Text = "=" },
      new() { Value = "ne", Text = "\u2260" },
      new() { Value = "gt", Text = ">" },
      new() { Value = "lt", Text = "<" },
      new() { Value = "ge", Text = "\u2265" },
      new() { Value = "le", Text = "\u2264" },
  ];

  ConditionDefinition _selectedDefinition;
  ConditionDefinition[] _lvalueDefinitions;

  public ConditionConstructor(UiFactory uiFactory) : base(uiFactory) {
    SignalSelector = new ArgumentConstructor(uiFactory);
    SignalSelector.OnStringValueChanged += (_, _) => SetArgument(SignalSelector.Value);
    OperatorSelector = uiFactory.CreateSimpleDropdown();
    ValueSelector = new ArgumentConstructor(uiFactory);

    Root = MakeRow(uiFactory.T(ConditionLabelLocKey), SignalSelector.Root, OperatorSelector, ValueSelector.Root);
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
    OperatorSelector.Items =
        _selectedDefinition.Argument.ValueType == ScriptValue.TypeEnum.String ? StringOperators : NumberOperators;
    OperatorSelector.ToggleDisplayStyle(true);
    ValueSelector.SetDefinition(_selectedDefinition.Argument);
    ValueSelector.Root.ToggleDisplayStyle(true);
  }

  #endregion
}
