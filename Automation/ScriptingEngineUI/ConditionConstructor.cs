// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

class ConditionConstructor : BaseConstructor {
  public override VisualElement Root { get; }

  public struct ConditionDefinition {
    public DropdownItem<string> Argument;
    public DropdownItem<Operands.OperandType>[] Operands;
    public DropdownItem<string>[] ArgTypes;
  }

  readonly ArgumentConstructor _argumentSelector;
  readonly SimpleDropdown<Operands.OperandType> _operand;
  readonly ArgumentConstructor _valueSelector;

  ConditionDefinition[] _lvalueDefinitions;

  public ConditionConstructor(UiFactory uiFactory) : base(uiFactory) {
    _argumentSelector = new ArgumentConstructor(uiFactory);
    _argumentSelector.OnStringValueChanged += (_, _) => SetArgument(_argumentSelector.StringValue);
    _operand = uiFactory.CreateValueDropdown<Operands.OperandType>((_, _) => {});
    _valueSelector = new ArgumentConstructor(uiFactory);

    Root = MakeRow("Если", _argumentSelector.Root, _operand.DropdownElement, _valueSelector.Root);
  }

  public void SetDefinitions(ConditionDefinition[] lvalueDef) {
    _lvalueDefinitions = lvalueDef;
    _argumentSelector.SetDefinitions(lvalueDef.Select(x => x.Argument).ToArray());
    SetArgument(_lvalueDefinitions[0].Argument.Value);
  }

  void SetArgument(string argument) {
    if (argument == null) {
      _operand.DropdownElement.ToggleDisplayStyle(false);
      _valueSelector.Root.ToggleDisplayStyle(false);
      return;
    }
    var def = _lvalueDefinitions.FirstOrDefault(x => x.Argument.Value == argument);
    _operand.Items = def.Operands;
    _operand.DropdownElement.ToggleDisplayStyle(def.Operands.Length > 1);
    _valueSelector.SetDefinitions(def.ArgTypes);
    _valueSelector.Root.ToggleDisplayStyle(true);
  }
}
