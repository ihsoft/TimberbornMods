// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;

using IgorZ.TimberDev.UI;
using TimberApi.UIPresets.Buttons;
using Timberborn.BaseComponentSystem;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RulesEditorDialog : ILoadableSingleton {

  public List<RuleConstructor> Rules { get; } = [];
  public VisualElement Content { get; private set; }

  readonly UiFactory _uiFactory;
  readonly ITooltipRegistrar _tooltipRegistrar;

  RulesEditorDialog(UiFactory uiFactory, ITooltipRegistrar tooltipRegistrar) {
    _uiFactory = uiFactory;
    _tooltipRegistrar = tooltipRegistrar;
  }

  public void Load() {
    var addRuleButton = _uiFactory.CreateButton("IgorZ.Automation.Scripting.Editor.AddRuleBtn", AddNewRule);
    //FIXME: wrap into a scroll view.
    Content = new VisualElement {
        style = {
            minHeight = 600,//FIXME: make it dynamic from the resolution.
        },
    };
    Content.Add(addRuleButton);
  }

  ConditionConstructor.ConditionDefinition[] _lvalueDefinitions;
  ActionConstructor.ActionDefinition[] _actionDefinitions;

  public void SetActiveBuilding(AutomationBehavior behavior) {
    _activeBuilding = behavior;
  }

  AutomationBehavior _activeBuilding;

  void AddNewRule() {
    var ruleConstructor = new RuleConstructor(_uiFactory);
    PopulateTestData(ruleConstructor);
    //FIXME:
    DebugEx.Warning("*** setting conditions");
    PopulateConstructor(ruleConstructor);
    Rules.Add(ruleConstructor);

    var wrapper = new VisualElement {
        style = {
            flexDirection = FlexDirection.Row,
            alignItems = Align.Center,
        },
    };

    var deleteBtn = _uiFactory.UiBuilder.Create<CrossButton>().BuildAndInitialize();
    deleteBtn.style.marginRight = 10;
    _tooltipRegistrar.Register(deleteBtn, _uiFactory.T("IgorZ.Automation.Scripting.Editor.DeleteRuleBtn"));
    deleteBtn.clicked += () => {
      Rules.Remove(ruleConstructor);
      wrapper.RemoveFromHierarchy();
    };
    wrapper.Add(deleteBtn);
    wrapper.Add(ruleConstructor.Root);
    Content.Insert(Content.childCount - 1, wrapper);
  }

  void PopulateConstructor(RuleConstructor ruleConstructor) {
    // var triggers = ScriptingService.Instance.GetTriggersForBuilding(_activeBuilding);
    // var conditions = triggers.Select(t => new ConditionConstructor.ConditionDefinition {
    //     Argument = (t.FullName, t.DisplayName),
    //     Operands = t.ValueType.ArgumentType == IScriptable.ArgumentDef.Type.String
    //         ? Operands.ToDropdownItems([Operands.OperandType.Equal, Operands.OperandType.NotEqual])
    //         : Operands.ToDropdownItems(Operands.All),
    //     ArgTypes = t.ValueType.Options, //FIXME: can be number?
    // });
    // ruleConstructor.ConditionConstructor.SetDefinitions(conditions.ToArray());
  }

  //FIXME: make it real!
  void PopulateTestData(RuleConstructor ruleConstructor) {
    ruleConstructor.ConditionConstructor.SetDefinitions([
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Weather.IsDrought", "сезон: засуха"),
            Operands = [
                Operands.ToDropdownItem(Operands.OperandType.Equal),
            ],
            ArgTypes = [
                ("True", "true"),
            ],
        },
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Weather.IsBadtide", "сезон: плохая вода"),
            Operands = [
                Operands.ToDropdownItem(Operands.OperandType.Equal),
            ],
            ArgTypes = [
                ("True", "true"),
            ],
        },
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Weather.IsTemperate", "сезон: умеренный"),
            Operands = [
                Operands.ToDropdownItem(Operands.OperandType.Equal),
            ],
            ArgTypes = [
                ("True", "true"),
            ],
        },
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Population.Bots", "население: боты"),
            Operands = Operands.ToDropdownItems(Operands.All),
            ArgTypes = [
                (ArgumentConstructor.NumberTypeName, "значение"),
            ],
        },
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Population.Beavers", "население: бобры"),
            Operands = Operands.ToDropdownItems(Operands.All),
            ArgTypes = [
                (ArgumentConstructor.NumberTypeName, "значение"),
            ],
        },
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Inventory.GoodId.Water", "товар: 'Вода'"),
            Operands = Operands.ToDropdownItems(Operands.All),
            ArgTypes = [
                (ArgumentConstructor.NumberTypeName, "значение"),
            ],
        },
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Inventory.GoodId.Logs", "товар: 'Дерево'"),
            Operands = Operands.ToDropdownItems(Operands.All),
            ArgTypes = [
                (ArgumentConstructor.NumberTypeName, "значение"),
            ],
        },
    ]);

    //FIXME: get this from the building via interfaces/annotated methods.
    ruleConstructor.ThenActionConstructor.SetDefinitions([
        new ActionConstructor.ActionDefinition {
            Action = ("Floodgate.SetHeight", "выставить высоту затвора в"),
            ArgTypes = [
                (ArgumentConstructor.NumberTypeName, "number-loc"),
            ],
        },
        new ActionConstructor.ActionDefinition {
            Action = ("Pausable.Pause", "остановить работу здания"),
        },
        new ActionConstructor.ActionDefinition {
            Action = ("Pausable.Resume", "продолжить работу здания"),
        },
    ]);
  }
}