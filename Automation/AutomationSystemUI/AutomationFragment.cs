// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngineUI;
using IgorZ.Automation.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine.UIElements;

namespace IgorZ.Automation.AutomationSystemUI;

sealed class AutomationFragment : IEntityPanelFragment {

  const string RuleTextLocKey = "IgorZ.Automation.AutomationFragment.RuleTextLocKey";
  const string RuleRowTemplate = "IgorZ.Automation/FragmentRule";
  const string FragmentResource = "IgorZ.Automation/EntityPanelFragment";

  const string RulesPanelName = "RulesPanel";
  const string RulesListName = "RulesList";
  const string RulesAdjustmentControlsName = "RulesAdjustmentControls";
  const string AddRulesButtonName = "AddRulesButton";
  const string EditRulesButtonName = "EditRulesButton";
  const string CopyRulesButtonName = "CopyRulesButton";

  // Scriptable components to ignore when checking for the controls' visibility.
  static readonly List<string> GlobalActions = [
      "Debug.", "Weather.", "Signals.",
  ];

  readonly UiFactory _uiFactory;
  readonly RulesEditorDialog _rulesEditorDialog;
  readonly CopyRulesTool _copyRulesTool;
  readonly EntityPanelSettings _entityPanelSettings;
  readonly ScriptingService _scriptingService;

  VisualElement _root;
  VisualElement _rulesList;
  Button _addRulesButton;

  AutomationBehavior _automationBehavior;

  int _automationBehaviorVersion = -1;

  AutomationFragment(UiFactory uiFactory, RulesEditorDialog rulesEditorDialog, CopyRulesTool copyRulesTool,
                     EntityPanelSettings entityPanelSettings, ScriptingService scriptingService) {
    _uiFactory = uiFactory;
    _rulesEditorDialog = rulesEditorDialog;
    _copyRulesTool = copyRulesTool;
    _entityPanelSettings = entityPanelSettings;
    _scriptingService = scriptingService;
  }

  public VisualElement InitializeFragment() {
    _root = _uiFactory.LoadVisualElement(FragmentResource);
    _rulesList = _root.Q(RulesListName);

    _addRulesButton = _root.Q<Button>(AddRulesButtonName);
    _addRulesButton.clicked += () => _rulesEditorDialog.Show(_automationBehavior, null);
    _root.Q<Button>(EditRulesButtonName).clicked += () => _rulesEditorDialog.Show(_automationBehavior, null);
    _root.Q<Button>(CopyRulesButtonName).clicked += () => _copyRulesTool.StartTool(_automationBehavior);

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _automationBehavior = entity.GetComponentFast<AutomationBehavior>();
    if (!_automationBehavior) {
      return;
    }
    if (!_automationBehavior.HasActions && !_entityPanelSettings.AlwaysShowAddRulesButton.Value) {
      var buildingHasEffects =
          _scriptingService.GetSignalNamesForBuilding(_automationBehavior).Any(x => !GlobalActions.Any(x.StartsWith))
          || _scriptingService.GetActionNamesForBuilding(_automationBehavior).Any(x => !GlobalActions.Any(x.StartsWith));
      if (!buildingHasEffects) {
        _automationBehavior = null;
        return;
      }
    }
    _automationBehaviorVersion = -1;
    _root.ToggleDisplayStyle(true);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
  }

  public void UpdateFragment() {
    if (!_automationBehavior || _automationBehavior.StateVersion == _automationBehaviorVersion) {
      return;
    }
    _automationBehaviorVersion = _automationBehavior.StateVersion;

    _root.ToggleDisplayStyle(visible: true);
    _root.Q(RulesPanelName).ToggleDisplayStyle(_automationBehavior.HasActions);
    _root.Q(RulesAdjustmentControlsName).ToggleDisplayStyle(_automationBehavior.HasActions);
    _addRulesButton.ToggleDisplayStyle(!_automationBehavior.HasActions);
    if (!_automationBehavior.HasActions) {
      return;
    }
    _rulesList.Clear();
    foreach (var action in _automationBehavior.Actions) {
      var row = _uiFactory.LoadVisualElement(RuleRowTemplate);
      string conditionText;
      if (_entityPanelSettings.RulesDescriptionStyle == EntityPanelSettings.DescriptionStyle.HumanReadable
          || action.Condition is not ScriptedCondition scriptedCondition) {
        conditionText = action.Condition.UiDescription;
      } else {
        conditionText = CommonFormats.HighlightYellow(scriptedCondition.Expression);
        if (_entityPanelSettings.RulesDescriptionStyle == EntityPanelSettings.DescriptionStyle.ScriptShort) {
          conditionText = ShortenNames(conditionText);
        }
      }
      string actionText;
      if (_entityPanelSettings.RulesDescriptionStyle == EntityPanelSettings.DescriptionStyle.HumanReadable
          || action is not ScriptedAction scriptedAction) {
        actionText = action.UiDescription;
      } else {
        actionText = CommonFormats.HighlightYellow(scriptedAction.Expression);
        if (_entityPanelSettings.RulesDescriptionStyle == EntityPanelSettings.DescriptionStyle.ScriptShort) {
          actionText = ShortenNames(actionText);
        }
      }
      row.Q<Label>("Content").text = _uiFactory.T(RuleTextLocKey, conditionText, actionText);
      row.SetEnabled(action.Condition.IsActive);
      _rulesList.Add(row);
    }
  }

  string ShortenNames(string description) {
    return _scriptingService.GetScriptableNames()
        .Aggregate(description, (current, scriptableName) => current.Replace(" " + scriptableName + ".", " "));
  }
}
