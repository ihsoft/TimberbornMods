// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngineUI;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.UI;

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

  readonly UiFactory _uiFactory;
  readonly RulesEditorDialog _rulesEditorDialog;
  readonly CopyRulesTool _copyRulesTool;

  VisualElement _root;
  VisualElement _rulesList;
  Button _addRulesButton;

  AutomationBehavior _automationBehavior;

  AutomationFragment(UiFactory uiFactory, RulesEditorDialog rulesEditorDialog, CopyRulesTool copyRulesTool) {
    _uiFactory = uiFactory;
    _rulesEditorDialog = rulesEditorDialog;
    _copyRulesTool = copyRulesTool;
  }

  public VisualElement InitializeFragment() {
    _root = _uiFactory.LoadVisualElement(FragmentResource);
    _rulesList = _root.Q(RulesListName);

    _addRulesButton = _root.Q<Button>(AddRulesButtonName);
    _addRulesButton.clicked += () => _rulesEditorDialog.Show(_automationBehavior, UpdateView);
    _root.Q<Button>(EditRulesButtonName).clicked += () => _rulesEditorDialog.Show(_automationBehavior, UpdateView);
    _root.Q<Button>(CopyRulesButtonName).clicked += () => _copyRulesTool.StartTool(_automationBehavior);

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
    _root.Q(RulesPanelName).ToggleDisplayStyle(_automationBehavior.HasActions);
    _root.Q(RulesAdjustmentControlsName).ToggleDisplayStyle(_automationBehavior.HasActions);
    _addRulesButton.ToggleDisplayStyle(!_automationBehavior.HasActions);
    if (!_automationBehavior.HasActions) {
      return;
    }
    _rulesList.Clear();
    foreach (var action in _automationBehavior.Actions) {
      var row = _uiFactory.LoadVisualElement(RuleRowTemplate);
      row.Q<Label>("Content").text = _uiFactory.T(RuleTextLocKey, action.Condition.UiDescription, action.UiDescription);
      _rulesList.Add(row);
    }
  }
}