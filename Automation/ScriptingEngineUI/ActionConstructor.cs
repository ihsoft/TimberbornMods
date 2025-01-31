// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
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

  public ActionConstructor(UiFactory uiFactory) : base(uiFactory) {
    //FIXME; get width from caller.
    _actionSelector = uiFactory.CreateValueDropdown<string>((_, action) => SetAction(action), width: 250);
    _argumentConstructor = new ArgumentConstructor(uiFactory);
    Root = MakeRow(uiFactory.T(ActionLabelLocKey), _actionSelector.DropdownElement, _argumentConstructor.Root);
  }

  public void SetDefinitions(IEnumerable<ActionDefinition> actionDefinitions) {
    _actionDefinitions = actionDefinitions.ToArray();
    _actionSelector.Items = _actionDefinitions.Select(def => def.Action).ToArray();
    SetAction(_actionDefinitions[0].Action.Value);
  }

  public string Validate() {
    if (_selectedAction.ArgumentType == ScriptValue.TypeEnum.Number) {
      return _argumentConstructor.CheckInputForNumber();
    }
    if (_selectedAction.ArgumentType == ScriptValue.TypeEnum.String) {
      return _argumentConstructor.CheckInputForString();
    }
    return null;
  }

  public string GetScript() {
    var action = _actionSelector.Value;
    var def = _actionDefinitions.First(x => x.Action.Value == action);
    return def.ArgumentOptions == null
        ? $"(act {action})"
        : $"(act {action} {PrepareConstantValue(_argumentConstructor.Value, def.ArgumentType)})";
  }

  #endregion

  ActionDefinition _selectedAction;
  ActionDefinition[] _actionDefinitions;

  readonly SimpleDropdown<string> _actionSelector;
  readonly ArgumentConstructor _argumentConstructor;

  void SetAction(string action) {
    _selectedAction = _actionDefinitions.First(def => def.Action.Value == action);
    if (_selectedAction.ArgumentOptions == null) {
      _argumentConstructor.Root.ToggleDisplayStyle(false);
    } else {
      _argumentConstructor.Root.ToggleDisplayStyle(true);
      _argumentConstructor.SetDefinitions(_selectedAction.ArgumentOptions);
    }
  }
}
