// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Text;
using Automation.AutomationSystem;
using IgorZ.TimberDev.UI;
using TimberApi.UIBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Automation.UI {

sealed class AutomationFragment : IEntityPanelFragment {
  const string RulesAreaCaptionTextLocKey = "IgorZ.Automation.AutomationFragment.RulesAreaCaptionTextLocKey";
  const string RuleTextLocKey = "IgorZ.Automation.AutomationFragment.RuleTextLocKey";

  readonly UiFactory _uiFactory;

  VisualElement _root;
  Label _caption;
  Label _rulesList;

  public AutomationFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  public VisualElement InitializeFragment() {
    // var presets = _builder.Presets();
    // _caption = presets.Labels().Label(color: Color.cyan);
    // _rulesList = presets.Labels().GameText();
    //
    // UIFragmentBuilder uIFragmentBuilder = _builder.CreateFragmentBuilder()
    //     .AddComponent(_caption)
    //     .AddComponent(_rulesList);
    // _root = uIFragmentBuilder.BuildAndInitialize();
    // _root.ToggleDisplayStyle(visible: false);
    // return _root;
    _caption = _uiFactory.CreateLabel();
    _caption.style.color = Color.cyan;

    _rulesList = _uiFactory.CreateLabel();

    _root = _uiFactory.CreateCenteredPanelFragmentBuilder()
        .AddComponent(_caption).AddComponent(_rulesList)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    var component = entity.GetComponentFast<AutomationBehavior>();
    if (!component || !component.HasActions) {
      return;
    }
    var sortedActions = new List<IAutomationAction>();
    foreach (var action in component.Actions) {
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
      rules.Append(_uiFactory.Loc.T(RuleTextLocKey, action.Condition.UiDescription, action.UiDescription));
    }
    _caption.text = _uiFactory.Loc.T(RulesAreaCaptionTextLocKey);
    _rulesList.text = rules.ToString();
    _root.ToggleDisplayStyle(visible: true);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
  }

  public void UpdateFragment() {
  }
}
}
