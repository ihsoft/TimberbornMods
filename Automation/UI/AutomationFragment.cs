// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Text;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngineUI;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.UI;

sealed class AutomationFragment : IEntityPanelFragment {
  const string RulesAreaCaptionTextLocKey = "IgorZ.Automation.AutomationFragment.RulesAreaCaptionTextLocKey";
  const string RuleTextLocKey = "IgorZ.Automation.AutomationFragment.RuleTextLocKey";
  const string AddRulesBtnCaptionLocKey = "IgorZ.Automation.AutomationFragment.AddRulesBtn";
  const string EditRulesBtnCaptionLocKey = "IgorZ.Automation.AutomationFragment.EditRulesBtn";

  readonly UiFactory _uiFactory;
  readonly RulesEditorDialog _rulesEditorDialog;

  VisualElement _root;
  Label _caption;
  Label _rulesList;
  Button _addRulesButton;
  Button _addTestRuleButton;

  AutomationBehavior _automationBehavior;

  AutomationFragment(UiFactory uiFactory, RulesEditorDialog rulesEditorDialog) {
    _uiFactory = uiFactory;
    _rulesEditorDialog = rulesEditorDialog;
  }

  public VisualElement InitializeFragment() {
    _caption = _uiFactory.CreateLabel();
    _caption.style.color = Color.cyan;

    _rulesList = _uiFactory.CreateLabel();

    _addRulesButton = _uiFactory.CreateButton(
        AddRulesBtnCaptionLocKey, () => _rulesEditorDialog.Show(_automationBehavior, UpdateView));
    //FIXME: this is a debug
    _addTestRuleButton = _uiFactory.CreateButton("AddTestRuleButton", AddTestRule);

    _root = _uiFactory.CreateCenteredPanelFragmentBuilder()
        .AddComponent(_caption)
        .AddComponent(_rulesList)
        .AddComponent(_addRulesButton)
        .AddComponent(_addTestRuleButton)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _automationBehavior = entity.GetComponentFast<AutomationBehavior>();
    UpdateView();
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
  }

  public void UpdateFragment() {
  }

  void UpdateView() {
    if (!_automationBehavior) {
      _root.ToggleDisplayStyle(false);
      return;
    }
    _root.ToggleDisplayStyle(visible: true);
    _caption.ToggleDisplayStyle(_automationBehavior.HasActions);
    _rulesList.ToggleDisplayStyle(_automationBehavior.HasActions);
    if (!_automationBehavior.HasActions) {
      _addRulesButton.text = _uiFactory.T(AddRulesBtnCaptionLocKey);
      return;
    }
    _addRulesButton.text = _uiFactory.T(EditRulesBtnCaptionLocKey);
    var sortedActions = new List<IAutomationAction>();
    foreach (var action in _automationBehavior.Actions) {
      var insertPos = 0;
      while (insertPos < sortedActions.Count) {
        if (string.CompareOrdinal(sortedActions[insertPos].TemplateFamily, action.TemplateFamily) > 0) {
          break;
        }
        insertPos++;
      }
      sortedActions.Insert(insertPos, action);
    }
    var rules = new StringBuilder();
    var actionsAdded = 0;
    foreach (var action in sortedActions) {
      if (actionsAdded++ > 0) {
        rules.AppendLine();
      }
      rules.Append(SpecialStrings.RowStarter);
      rules.Append(_uiFactory.T(RuleTextLocKey, action.Condition.UiDescription, action.UiDescription));
    }
    _caption.text = _uiFactory.T(RulesAreaCaptionTextLocKey);
    _rulesList.text = rules.ToString();
    _root.ToggleDisplayStyle(visible: true);
  }

  void AddTestRule() {
    var condition = new ScriptedCondition();
    condition.SetExpression("(and (eq (sig Weather.Season) 'drought') (or (eq (sig Weather.Season) 'temperate') (eq (sig Weather.Season) 'badtide')))");
    //condition.SetExpression("(eq (sig Weather.Season) 'drought')");
    var action = new ScriptedAction();
    //action.SetExpression("(act Floodgate.SetHeight 120)");
    action.SetExpression("(act Floodgate.SetHeight -500)");
    _automationBehavior.AddRule(condition, action);
    UpdateView();
  }
}