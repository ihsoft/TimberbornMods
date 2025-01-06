// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.TimberDev.UI;
using TimberApi.UIPresets.Buttons;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class TestUI : IPanelController, ILoadableSingleton {

  readonly PanelStack _panelStack;
  readonly VisualElementLoader _visualElementLoader;
  readonly UiFactory _uiFactory;
  readonly ITooltipRegistrar _tooltipRegistrar;

  readonly List<RuleConstructor> _ruleConstructors = [];
  VisualElement _root;
  VisualElement _ruleConstructorsContainer;

  TestUI(PanelStack panelStack, VisualElementLoader visualElementLoader, DropdownItemsSetter dropdownItemsSetter,
         UiFactory uiFactory, ITooltipRegistrar tooltipRegistrar) {
    DebugEx.Warning("*** TestUI");
    _panelStack = panelStack;
    _visualElementLoader = visualElementLoader;
    _uiFactory = uiFactory;
    _tooltipRegistrar = tooltipRegistrar;
  }

  public VisualElement GetPanel() {
    return _root;
  }

  public bool OnUIConfirmed() {
    DebugEx.Warning("*** OnUIConfirmed");
    return false;
  }

  public void OnUICancelled() {
    DebugEx.Warning("*** OnUICancelled");
  }

  VisualElement GetDialogBox() {
    var dialogBox = _visualElementLoader.LoadVisualElement("Options/SettingsBox");
    dialogBox.Q<Label>("DeveloperTestLabel").ToggleDisplayStyle(false);
    dialogBox.Q<VisualElement>("Developer").ToggleDisplayStyle(false);
    dialogBox.Q<ScrollView>("Content").Clear();
    return dialogBox;
  }

  public void Load() {
    _root = GetDialogBox();
    _root.style.position = Position.Absolute;
    _panelStack._root.Add(_root);

    var addRuleButton = _uiFactory.CreateButton("Add New Rule", AddNewRule);
    addRuleButton.text = "Добавить правило";//FIXME: loc

    _ruleConstructorsContainer = _root.Q<ScrollView>("Content");
    _ruleConstructorsContainer.Add(addRuleButton);
  }

  void AddNewRule() {
    var ruleConstructor = new RuleConstructor(_uiFactory);
    PopulateTestData(ruleConstructor);
    _ruleConstructors.Add(ruleConstructor);

    var wrapper = new VisualElement {
        style = {
            flexDirection = FlexDirection.Row,
            alignItems = Align.Center,
        }
    };

    var deleteBtn = _uiFactory.UiBuilder.Create<CrossButton>().BuildAndInitialize();
    deleteBtn.style.marginRight = 10;
    _tooltipRegistrar.Register(deleteBtn, "Удалить правило");//FIXME
    deleteBtn.clicked += () => {
      _ruleConstructors.Remove(ruleConstructor);
      wrapper.RemoveFromHierarchy();
    };
    wrapper.Add(deleteBtn);
    wrapper.Add(ruleConstructor.Root);
    _ruleConstructorsContainer.Insert(_ruleConstructorsContainer.childCount - 1, wrapper);
  }

  //FIXME: make it real!
  void PopulateTestData(RuleConstructor ruleConstructor) {
    ruleConstructor.ConditionConstructor.SetDefinitions([
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Weather.IsDrought", "сезон: засуха"),
            Operands = [
                Operands.ToDropdownItem(Operands.Type.Equal),
            ],
            ArgTypes = [
                ("True", "true"),
            ],
        },
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Weather.IsBadtide", "сезон: плохая вода"),
            Operands = [
                Operands.ToDropdownItem(Operands.Type.Equal),
            ],
            ArgTypes = [
                ("True", "true"),
            ],
        },
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Weather.IsTemperate", "сезон: умеренный"),
            Operands = [
                Operands.ToDropdownItem(Operands.Type.Equal),
            ],
            ArgTypes = [
                ("True", "true"),
            ],
        },
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Population.Bots", "количество ботов"),
            Operands = Operands.ToDropdownItems(Operands.All),
            ArgTypes = [
                (ArgumentConstructor.NumberTypeName, "значение"),
            ],
        },
        new ConditionConstructor.ConditionDefinition {
            Argument = ("Population.Beavers", "количество бобров"),
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

    ruleConstructor.ElseActionConstructor.SetDefinitions([
        new ActionConstructor.ActionDefinition {
            Action = ("Nothing", "НЕ НАЗНАЧЕНО"),
        },
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
            Action = ("Pausable.Resume", "возобновить работу здания"),
        },
    ]);
  }
}