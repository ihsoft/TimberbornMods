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
    public ScriptValue.TypeEnum ArgumentType { get; init; }
    public DropdownItem<string>[] ArgumentOptions { get; init; }
  }

  public override VisualElement Root { get; }
  public readonly SimpleDropdown<string> ActionSelector;
  public readonly ArgumentConstructor ArgumentConstructor;

  public ActionConstructor(UiFactory uiFactory) : base(uiFactory) {
    //FIXME; get width from caller.
    ActionSelector = uiFactory.CreateValueDropdown<string>((_, action) => SetAction(action), width: 250);
    ArgumentConstructor = new ArgumentConstructor(uiFactory);
    Root = MakeRow(uiFactory.T(ActionLabelLocKey), ActionSelector.DropdownElement, ArgumentConstructor.Root);
  }

  public void SetDefinitions(IEnumerable<ActionDefinition> actionDefinitions) {
    _actionDefinitions = actionDefinitions.ToArray();
    ActionSelector.Items = _actionDefinitions.Select(def => def.Action).ToArray();
    SetAction(_actionDefinitions[0].Action.Value);
  }

  public string Validate() {
    if (_selectedAction.ArgumentType == ScriptValue.TypeEnum.Number) {
      return ArgumentConstructor.CheckInputForNumber();
    }
    if (_selectedAction.ArgumentType == ScriptValue.TypeEnum.String) {
      return ArgumentConstructor.CheckInputForString();
    }
    return null;
  }

  public string GetScript() {
    var action = ActionSelector.Value;
    var def = _actionDefinitions.First(x => x.Action.Value == action);
    return def.ArgumentOptions == null
        ? $"(act {action})"
        : $"(act {action} {PrepareConstantValue(ArgumentConstructor.Value, def.ArgumentType)})";
  }

  #endregion

  ActionDefinition _selectedAction;
  ActionDefinition[] _actionDefinitions;

  void SetAction(string action) {
    _selectedAction = _actionDefinitions.First(def => def.Action.Value == action);
    if (_selectedAction.ArgumentOptions == null) {
      ArgumentConstructor.Root.ToggleDisplayStyle(false);
    } else {
      ArgumentConstructor.Root.ToggleDisplayStyle(true);
      ArgumentConstructor.SetDefinitions(_selectedAction.ArgumentOptions);
    }
  }
}
