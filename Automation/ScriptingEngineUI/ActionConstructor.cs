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

sealed class ActionConstructor : BaseConstructor {

  const string ActionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ActionLabel";

  #region API

  public record ActionDefinition {
    public DropdownItem<string> Action { get; init; }
    public (ScriptValue.TypeEnum ValueType, DropdownItem<string>[] Options)[] Arguments { get; init; }
  }

  public override VisualElement Root { get; }
  public readonly ResizableDropdownElement ActionSelector;
  public readonly ArgumentConstructor ArgumentConstructor;

  public void SetDefinitions(IEnumerable<ActionDefinition> actionDefinitions) {
    _actionDefinitions = actionDefinitions.ToArray();
    ActionSelector.Items = _actionDefinitions.Select(def => def.Action).ToArray();
    SetAction(_actionDefinitions[0].Action.Value);
  }

  public string Validate() {
    return _selectedAction.Arguments.Length switch {
        0 => null,
        1 => _selectedAction.Arguments[0].ValueType switch {
            ScriptValue.TypeEnum.Number => ArgumentConstructor.CheckInputForNumber(),
            ScriptValue.TypeEnum.String => ArgumentConstructor.CheckInputForString(),
            _ => null,
        },
        _ => throw new System.NotImplementedException("Multiple arguments are not supported yet"),
    };
  }

  public string GetScript() {
    var action = ActionSelector.SelectedValue;
    var def = _actionDefinitions.First(x => x.Action.Value == action);
    if (def.Arguments.Length == 0) {
      return $"(act {action})";
    }
    var script = "(act " + action;
    for (var i = 0; i < def.Arguments.Length; i++) {
      script += " " + PrepareConstantValue(ArgumentConstructor.Value, def.Arguments[i].ValueType);
    }
    return script + ")";
  }

  #endregion

  #region Implementation

  ActionDefinition _selectedAction;
  ActionDefinition[] _actionDefinitions;

  public ActionConstructor(UiFactory uiFactory) : base(uiFactory) {
    ActionSelector = uiFactory.CreateSimpleDropdown(SetAction);
    ArgumentConstructor = new ArgumentConstructor(uiFactory);
    Root = MakeRow(uiFactory.T(ActionLabelLocKey), ActionSelector, ArgumentConstructor.Root);
  }

  void SetAction(string action) {
    _selectedAction = _actionDefinitions.First(def => def.Action.Value == action);
    if (_selectedAction.Arguments.Length == 0) {
      ArgumentConstructor.Root.ToggleDisplayStyle(false);
    } else if (_selectedAction.Arguments.Length == 1) {
      ArgumentConstructor.Root.ToggleDisplayStyle(true);
      ArgumentConstructor.SetDefinitions(_selectedAction.Arguments[0].Options);
    } else {
      throw new System.NotImplementedException("Multiple arguments are not supported yet");
    }
  }

  #endregion
}
