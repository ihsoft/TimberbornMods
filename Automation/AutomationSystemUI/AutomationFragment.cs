// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

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
using Timberborn.TooltipSystem;
using UnityEngine.UIElements;

namespace IgorZ.Automation.AutomationSystemUI;

sealed class AutomationFragment : IEntityPanelFragment {

  const string RuleRowTemplate = "IgorZ.Automation/FragmentRule";
  const string SignalRowTemplate = "IgorZ.Automation/FragmentSignal";
  const string FragmentResource = "IgorZ.Automation/EntityPanelFragment";
  
  const string RulesCountTextLocKey = "IgorZ.Automation.AutomationFragment.RulesCountTextLocKey";
  const string ConditionTextLocKey = "IgorZ.Automation.AutomationFragment.RuleConditionTextLocKey";
  const string ActionTextLocKey = "IgorZ.Automation.AutomationFragment.RuleActionTextLocKey";
  const string SetupRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.SetupRulesBtnHint";
  const string CopyRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.CopyRulesBtnHint";
  const string ClearRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.ClearRulesBtnHint";

  const string SignalsCountTextLocKey = "IgorZ.Automation.AutomationFragment.SignalsCountTextLocKey";
  const string SetupSignalsBtnHintLocKey = "IgorZ.Automation.AutomationFragment.SetupSignalsBtnHint";
  const string ClearSignalsBtnHintLocKey = "IgorZ.Automation.AutomationFragment.ClearSignalsBtnHint";

  readonly UiFactory _uiFactory;
  readonly RulesEditorDialog _rulesEditorDialog;
  readonly SignalsEditorDialog _signalsEditorDialog;
  readonly CopyRulesTool _copyRulesTool;
  readonly ScriptingService _scriptingService;
  readonly ScriptingRulesUIHelper _scriptingRulesUIHelper;
  readonly ITooltipRegistrar _tooltipRegistrar;

  VisualElement _root;

  VisualElement _rulesList;
  bool _rulesListFolded = true;
  Label _rulesCountLabel;
  Button _foldRulesButton;
  Button _unfoldRulesButton;
  Button _copyRulesButton;

  VisualElement _signalsList;
  bool _signalsListFolded = true;
  Label _signalsCountLabel;
  Button _foldSignalsButton;
  Button _unfoldSignalsButton;

  AutomationBehavior _automationBehavior;
  int _automationBehaviorVersion = -1;

  AutomationFragment(UiFactory uiFactory, RulesEditorDialog rulesEditorDialog, SignalsEditorDialog signalsEditorDialog,
                     CopyRulesTool copyRulesTool, ScriptingService scriptingService,
                     ScriptingRulesUIHelper scriptingRulesUIHelper, ITooltipRegistrar tooltipRegistrar) {
    _uiFactory = uiFactory;
    _rulesEditorDialog = rulesEditorDialog;
    _signalsEditorDialog = signalsEditorDialog;
    _copyRulesTool = copyRulesTool;
    _scriptingService = scriptingService; //FIXME: move to the helper
    _scriptingRulesUIHelper = scriptingRulesUIHelper;
    _tooltipRegistrar = tooltipRegistrar;
  }

  Button _clearRulesButton;
  Button _setupRulesButton;
  Button _clearSignalsButton;

  public VisualElement InitializeFragment() {
    _root = _uiFactory.LoadVisualTreeAsset(FragmentResource);
    _rulesList = _root.Q2<VisualElement>("RulesList");
    _signalsList = _root.Q2<VisualElement>("SignalsList");

    // Setup signals fragment section.
    var setupSignalsButton = _root.Q2<Button>("SetupSignalsButton");
    setupSignalsButton.clicked += () => _signalsEditorDialog.WithUiHelper(_scriptingRulesUIHelper).Show();
    _tooltipRegistrar.RegisterLocalizable(setupSignalsButton, SetupSignalsBtnHintLocKey);
    _clearSignalsButton = _root.Q2<Button>("ClearSignalsButton");
    _tooltipRegistrar.RegisterLocalizable(_clearSignalsButton, ClearSignalsBtnHintLocKey);
    _clearSignalsButton.clicked += () => _scriptingRulesUIHelper.ClearSignalsOnBuilding();

    _signalsCountLabel = _root.Q2<Label>("SignalsCountLabel");
    _foldSignalsButton = _root.Q2<Button>("FoldSignalsButton");
    _unfoldSignalsButton = _root.Q2<Button>("UnfoldSignalsButton");
    _foldSignalsButton.clicked += () => SetSignalsListFolded(true);
    _unfoldSignalsButton.clicked += () => SetSignalsListFolded(false);

    // Setup rules fragment section.
    _setupRulesButton = _root.Q2<Button>("SetupRulesButton");
    _setupRulesButton.clicked += () => _rulesEditorDialog.WithUiHelper(_scriptingRulesUIHelper).Show();
    _tooltipRegistrar.RegisterLocalizable(_setupRulesButton, SetupRulesBtnHintLocKey);
    _copyRulesButton = _root.Q2<Button>("CopyRulesButton");
    _copyRulesButton.clicked += () => _copyRulesTool.StartTool(_automationBehavior);
    _tooltipRegistrar.RegisterLocalizable(_copyRulesButton, CopyRulesBtnHintLocKey);
    _clearRulesButton = _root.Q2<Button>("ClearRulesButton");
    _tooltipRegistrar.RegisterLocalizable(_clearRulesButton, ClearRulesBtnHintLocKey);
    _clearRulesButton.clicked += () => _scriptingRulesUIHelper.ClearRulesOnBuilding();

    _rulesCountLabel = _root.Q2<Label>("RulesCountLabel");
    _foldRulesButton = _root.Q2<Button>("FoldRulesButton");
    _unfoldRulesButton = _root.Q2<Button>("UnfoldRulesButton");
    _foldRulesButton.clicked += () => SetRulesListFolded(true);
    _unfoldRulesButton.clicked += () => SetRulesListFolded(false);

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _automationBehavior = entity.GetComponentFast<AutomationBehavior>();
    if (!_automationBehavior) {
      return;
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
    _scriptingRulesUIHelper.SetBuilding(_automationBehavior);
    SetRulesListFolded(_rulesListFolded);
    SetSignalsListFolded(_signalsListFolded);

    // Signals panel.
    var totalSignalsCount = _scriptingRulesUIHelper.BuildingSignalNames.Count;
    var exposedSignalsCount = _scriptingRulesUIHelper.ExposedSignalsCount;
    _root.Q2<VisualElement>("SignalsPanel").ToggleDisplayStyle(totalSignalsCount > 0);
    _signalsCountLabel.text = _uiFactory.T(SignalsCountTextLocKey, exposedSignalsCount, totalSignalsCount);
    _clearSignalsButton.SetEnabled(exposedSignalsCount > 0);
    _signalsList.Clear();
    foreach (var signalMapping in _scriptingRulesUIHelper.BuildingSignals) {
      if (signalMapping.ExportedSignalName == null) {
        continue;
      }
      var row = _uiFactory.LoadVisualElement(SignalRowTemplate);
      row.Q2<Label>("Source").text = signalMapping.Describe;
      row.Q2<Label>("Target").text = signalMapping.ExportedSignalName;
      if (signalMapping.Action != null) {
        row.Q2<VisualElement>("Container").SetEnabled(signalMapping.Action.Condition.IsActive);
      }
      _signalsList.Add(row);
    }

    // Rules panel.
    _rulesCountLabel.text = _uiFactory.T(RulesCountTextLocKey, _scriptingRulesUIHelper.BuildingRules.Count);
    _clearRulesButton.SetEnabled(_scriptingRulesUIHelper.BuildingRules.Count > 0);
    _copyRulesButton.SetEnabled(_scriptingRulesUIHelper.BuildingRules.Count > 0);
    _rulesList.Clear();
    foreach (var action in _scriptingRulesUIHelper.BuildingRules) {
      var row = _uiFactory.LoadVisualElement(RuleRowTemplate);
      _rulesList.Add(row);
      rowsAdded++;

      var conditionLabel = row.Q2<Label>("Condition");
      var actionLabel = row.Q2<Label>("Action");

      string conditionText;
      if (EntityPanelSettings.RulesDescriptionStyle == EntityPanelSettings.DescriptionStyle.HumanReadable
          || action.Condition is not ScriptedCondition scriptedCondition) {
        conditionText = action.Condition.UiDescription;
      } else {
        conditionText = scriptedCondition.Expression;
        if (EntityPanelSettings.RulesDescriptionStyle == EntityPanelSettings.DescriptionStyle.ScriptShort) {
          conditionText = ShortenNames(conditionText);
        }
      }
      conditionLabel.text = _uiFactory.T(ConditionTextLocKey, conditionText);

      string actionText;
      if (EntityPanelSettings.RulesDescriptionStyle == EntityPanelSettings.DescriptionStyle.HumanReadable
          || action is not ScriptedAction scriptedAction) {
        actionText = action.UiDescription;
      } else {
        actionText = scriptedAction.Expression;
        if (EntityPanelSettings.RulesDescriptionStyle == EntityPanelSettings.DescriptionStyle.ScriptShort) {
          actionText = ShortenNames(actionText);
        }
      }
      actionLabel.text = _uiFactory.T(ActionTextLocKey, actionText);
    }
  }

  string ShortenNames(string description) {
    return _scriptingService.GetScriptableNames()
        .Aggregate(description, (current, scriptableName) => current.Replace(" " + scriptableName + ".", " "));
  }

  void SetRulesListFolded(bool isFolded) {
    _rulesListFolded = isFolded;
    var actualFoldState = isFolded || _scriptingRulesUIHelper.BuildingRules.Count == 0;
    _rulesList.ToggleDisplayStyle(!actualFoldState);
    _foldRulesButton.ToggleDisplayStyle(!actualFoldState);
    _unfoldRulesButton.ToggleDisplayStyle(actualFoldState);
    _unfoldRulesButton.SetEnabled(_scriptingRulesUIHelper.BuildingRules.Count > 0);
  }

  void SetSignalsListFolded(bool isFolded) {
    _signalsListFolded = isFolded;
    var actualFoldState = _signalsListFolded || _scriptingRulesUIHelper.ExposedSignalsCount == 0;
    _signalsList.ToggleDisplayStyle(!actualFoldState);
    _foldSignalsButton.ToggleDisplayStyle(!actualFoldState);
    _unfoldSignalsButton.ToggleDisplayStyle(actualFoldState);
    _unfoldSignalsButton.SetEnabled(_scriptingRulesUIHelper.ExposedSignalsCount > 0);
  }
}
