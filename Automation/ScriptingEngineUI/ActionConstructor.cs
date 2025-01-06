// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ActionConstructor : BaseConstructor {
  public string Action {
    get => _selectedAction;
    set => _actionSelector.Value = value;
  }
  readonly string _selectedAction = "";

  public ArgumentValue ArgumentValue {
    get {
      if (_argumentConstructor.SelectedType == ArgumentConstructor.ArgumentType.Number) {
        return new ArgumentValue { NumberValue = _argumentConstructor.NumberValue };
      }
      return new ArgumentValue { StringValue = _argumentConstructor.StringValue };
    }
    set => _argumentConstructor.StringValue = value.StringValue;
  }

  public struct ActionDefinition {
    public DropdownItem<string> Action;
    public DropdownItem<string>[] ArgTypes;
  }

  ActionDefinition[] _actionDefinitions;

  public override VisualElement Root { get; }

  readonly SimpleDropdown<string> _actionSelector;
  readonly ArgumentConstructor _argumentConstructor;

  public ActionConstructor(UiFactory uiFactory, bool isInvertedCondition) : base(uiFactory) {
    _actionSelector = uiFactory.CreateValueDropdown<string>((_, action) => SetAction(action), width: 250);
    _argumentConstructor = new ArgumentConstructor(uiFactory);
    var actionText = isInvertedCondition ? "Иначе" : "Тогда";//FIXME: translate
    Root = MakeRow(actionText, _actionSelector.DropdownElement, _argumentConstructor.Root);
  }

  public void SetDefinitions(ActionDefinition[] actionDefinitions) {
    _actionDefinitions = actionDefinitions;
    _actionSelector.Items = actionDefinitions.Select(def => def.Action).ToArray();
    SetAction(actionDefinitions[0].Action.Value);
  }

  void SetAction(string action) {
    var actionDef = _actionDefinitions.First(def => def.Action.Value == action);
    if (actionDef.ArgTypes == null) {
      _argumentConstructor.Root.ToggleDisplayStyle(false);
    } else {
      _argumentConstructor.Root.ToggleDisplayStyle(true);
      _argumentConstructor.SetDefinitions(actionDef.ArgTypes);
    }
  }
}
