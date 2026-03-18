// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ActionConstructor : BaseConstructor {

  const string ActionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ActionLabel";
  const string ActionArgumentNameLocKey = "IgorZ.Automation.Scripting.Editor.ActionArgumentName";

  #region API

  public record ActionDefinition {
    public DropdownItem Name { get; init; }
    public ArgumentDefinition[] Arguments { get; init; }
  }

  public override VisualElement Root { get; }
  public readonly ResizableDropdownElement ActionSelector;

  public void SetDefinitions(IEnumerable<ActionDefinition> actionDefinitions) {
    _actionDefinitions = actionDefinitions.ToArray();
    ActionSelector.Items = _actionDefinitions.Select(def => def.Name).ToArray();
    SetAction(_actionDefinitions[0].Name.Value);
  }

  public void SetArguments(IList<ScriptValue> scriptValues) {
    if (scriptValues.Count != _selectedAction.Arguments.Length) {
      throw new ArgumentException(
          $"Expected exactly {_selectedAction.Arguments.Length} arguments, but got {scriptValues.Count}",
          nameof(scriptValues));
    }
    for (var i = 0; i < scriptValues.Count; i++) {
      _argumentConstructors[i].SetScriptValue(scriptValues[i]);
    }
  }

  public string Validate() {
    return _argumentConstructors.Select(x => x.Validate()).FirstOrDefault(err => err != null);
  }

  public string GetLispScript() {
    if (_selectedAction.Arguments.Length == 0) {
      return $"(act {_selectedAction.Name.Value})";
    }
    var script = "(act " + _selectedAction.Name.Value;
    foreach (var argumentConstructor in _argumentConstructors) {
      script += " " + argumentConstructor.GetScriptValue();
    }
    return script + ")";
  }

  #endregion

  #region Implementation

  static readonly Color ArgumentLabelColor = new(0.48f, 0.71f, 1f);

  readonly VisualElement _singleArgumentContainer;
  readonly VisualElement _multiArgumentContainer;
  readonly List<ArgumentConstructor> _argumentConstructors = [];

  ActionDefinition _selectedAction;
  ActionDefinition[] _actionDefinitions;

  public ActionConstructor(UiFactory uiFactory) : base(uiFactory) {
    ActionSelector = uiFactory.CreateSimpleDropdown(SetAction);
    _singleArgumentContainer = MakeRow();
    _multiArgumentContainer = new VisualElement {
        style = {
            flexDirection = FlexDirection.Column,
            alignItems = Align.FlexStart,
        },
    };
    Root = new VisualElement {
        style = {
            flexDirection = FlexDirection.Column,
            alignItems = Align.Stretch,
        },
    };
    Root.Add(MakeRow(uiFactory.T(ActionLabelLocKey), ActionSelector, _singleArgumentContainer));
    Root.Add(_multiArgumentContainer);
  }

  void SetAction(string action) {
    _selectedAction = _actionDefinitions.First(def => def.Name.Value == action);
    _argumentConstructors.Clear();
    _multiArgumentContainer.Clear();
    _singleArgumentContainer.Clear();
    var arguments = _selectedAction.Arguments;
    if (arguments.Length == 0) {
      return;
    }
    var pos = 0;
    foreach (var argumentDef in arguments) {
      var argumentConstructor = new ArgumentConstructor(UIFactory);
      argumentConstructor.SetDefinition(argumentDef);
      _argumentConstructors.Add(argumentConstructor);
      if (arguments.Length == 1) {
        _singleArgumentContainer.Add(argumentConstructor.Root);
      } else {
        var displayName = argumentDef.ValueDef.DisplayName ?? UIFactory.T(ActionArgumentNameLocKey, pos + 1);
        var labelContainer = MakeRow(displayName + ":");
        labelContainer.Q<Label>().style.color = ArgumentLabelColor;
        _multiArgumentContainer.Add(MakeRow(labelContainer, argumentConstructor.Root));
      }
      pos++;
    }
  }

  #endregion
}
