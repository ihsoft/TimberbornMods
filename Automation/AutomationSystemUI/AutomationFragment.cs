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
using Timberborn.TooltipSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.AutomationSystemUI;

sealed class AutomationFragment : IEntityPanelFragment {

  const string RuleRowTemplate = "IgorZ.Automation/FragmentRule";
  const string SignalRowTemplate = "IgorZ.Automation/FragmentSignal";
  const string FragmentResource = "IgorZ.Automation/EntityPanelFragment";
  
  const string ConditionTextLocKey = "IgorZ.Automation.AutomationFragment.RuleConditionTextLocKey";
  const string ActionTextLocKey = "IgorZ.Automation.AutomationFragment.RuleActionTextLocKey";
  const string RulesCountTextLocKey = "IgorZ.Automation.AutomationFragment.RulesCountTextLocKey";
  const string SignalsCountTextLocKey = "IgorZ.Automation.AutomationFragment.SignalsCountTextLocKey";

  const string SetupRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.SetupRulesBtnHint";
  const string CopyRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.CopyRulesBtnHint";
  const string ClearRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.ClearRulesBtnHint";


  // Scriptable components to ignore when checking for the controls' visibility.
  // FIXME: Addd "District.". Or not.
  static readonly List<string> GlobalActions = [
      "Debug.", "Weather.", "Signals.",
  ];

  readonly UiFactory _uiFactory;
  readonly RulesEditorDialog _rulesEditorDialog;
  readonly CopyRulesTool _copyRulesTool;
  readonly ScriptingService _scriptingService;
  readonly ScriptingRulesUIHelper _scriptingRulesUIHelper;
  readonly ITooltipRegistrar _tooltipRegistrar;

  VisualElement _root;

  VisualElement _rulesList;
  bool _rulesListFolded = true; //FXIME; settings
  Label _rulesCountLabel;
  Button _foldRulesButton;
  Button _unfoldRulesButton;
  Button _copyRulesButton;

  VisualElement _signalsList;
  bool _signalsListFolded = true; //FXIME; settings
  Label _signalsCountLabel;
  Button _foldSignalsButton;
  Button _unfoldSignalsButton;

  AutomationBehavior _automationBehavior;
  int _automationBehaviorVersion = -1;

  AutomationFragment(UiFactory uiFactory, RulesEditorDialog rulesEditorDialog, CopyRulesTool copyRulesTool,
                     ScriptingService scriptingService, ScriptingRulesUIHelper scriptingRulesUIHelper,
                     ITooltipRegistrar tooltipRegistrar) {
    _uiFactory = uiFactory;
    _rulesEditorDialog = rulesEditorDialog;
    _copyRulesTool = copyRulesTool;
    _scriptingService = scriptingService; //FIXME: move to the helper
    _scriptingRulesUIHelper = scriptingRulesUIHelper;
    _tooltipRegistrar = tooltipRegistrar;
  }

  public VisualElement InitializeFragment() {
    _root = _uiFactory.LoadVisualTreeAsset(FragmentResource);
    _rulesList = _root.Q2<VisualElement>("RulesList");
    _signalsList = _root.Q2<VisualElement>("SignalsList");

    var setupRulesButton = _root.Q2<Button>("SetupRulesButton");
    setupRulesButton.clicked += () => _rulesEditorDialog.Show(_automationBehavior, null);
    _tooltipRegistrar.RegisterLocalizable(setupRulesButton, SetupRulesBtnHintLocKey);

    _copyRulesButton = _root.Q2<Button>("CopyRulesButton");
    _copyRulesButton.clicked += () => _copyRulesTool.StartTool(_automationBehavior);
    _tooltipRegistrar.RegisterLocalizable(_copyRulesButton, CopyRulesBtnHintLocKey);

    //FIXME: store and change enabled state
    var clearRulesButton = _root.Q2<Button>("ClearRulesButton");
    _tooltipRegistrar.RegisterLocalizable(clearRulesButton, ClearRulesBtnHintLocKey);
    clearRulesButton.clicked += ClearAllRules;

    //FIXME: get setup button and bind opening dialog.

    //FIXME: store and change enabled state
    var clearSignalsButton = _root.Q2<Button>("ClearSignalsButton");
    _tooltipRegistrar.RegisterLocalizable(clearSignalsButton, ClearRulesBtnHintLocKey);
    clearSignalsButton.clicked += ClearAllSignals;

    _rulesCountLabel = _root.Q2<Label>("RulesCountLabel");
    _foldRulesButton = _root.Q2<Button>("FoldRulesButton");
    _unfoldRulesButton = _root.Q2<Button>("UnfoldRulesButton");
    _foldRulesButton.clicked += () => SetRulesListFolded(true);
    _unfoldRulesButton.clicked += () => SetRulesListFolded(false);

    _signalsCountLabel = _root.Q2<Label>("SignalsCountLabel");
    _foldSignalsButton = _root.Q2<Button>("FoldSignalsButton");
    _unfoldSignalsButton = _root.Q2<Button>("UnfoldSignalsButton");
    _foldSignalsButton.clicked += () => SetSignalsListFolded(true);
    _unfoldSignalsButton.clicked += () => SetSignalsListFolded(false);

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
    //FIXME
    DebugEx.Warning("*** AutomationFragment: updating UI for version change {0} => {1}", _automationBehavior.StateVersion, _automationBehaviorVersion);
    _automationBehaviorVersion = _automationBehavior.StateVersion;
    _scriptingRulesUIHelper.SetBuilding(_automationBehavior);
    SetRulesListFolded(_rulesListFolded);
    SetSignalsListFolded(_signalsListFolded);

    // Signals panel.
    _signalsCountLabel.text = _uiFactory.T(
        SignalsCountTextLocKey,
        _scriptingRulesUIHelper.ExposedSignalsCount, _scriptingRulesUIHelper.BuildingSignals.Count);
    _signalsList.Clear();
    foreach (var signal in _scriptingRulesUIHelper.BuildingSignals) {
      if (signal.ExportedSignalName == null) {
        continue;
      }
      var row = _uiFactory.LoadVisualElement(SignalRowTemplate);
      //FIXME: make coloring in UI template
      //FIXME: register "signal changed" callback to update the signal value
      row.Q2<Label>("Source").text = CommonFormats.HighlightGreen(signal.Describe);
      row.Q2<Label>("Target").text = CommonFormats.HighlightYellow(signal.ExportedSignalName);
      row.Q2<VisualElement>("Container").SetEnabled(signal.IsActive);
      _signalsList.Add(row);
    }

    // Rules panel.
    _rulesCountLabel.text = _uiFactory.T(RulesCountTextLocKey, _scriptingRulesUIHelper.RulesCount);
    _copyRulesButton.SetEnabled(_scriptingRulesUIHelper.RulesCount > 0);
    _rulesList.Clear();
    foreach (var action in _automationBehavior.Actions) {
      if (ScriptingRulesUIHelper.IsSignalMapping(action)) {
        continue;
      }
      var row = _uiFactory.LoadVisualElement(RuleRowTemplate);

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
      if (action.Condition.IsInErrorState) {
        conditionText = CommonFormats.HighlightRed(conditionText);
      } else if (action.Condition.ConditionState) {
        conditionText = CommonFormats.HighlightGreen(conditionText);
      } else {
        conditionText = CommonFormats.HighlightYellow(conditionText);
      }

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
      actionText = action.IsInErrorState
          ? CommonFormats.HighlightRed(actionText)
          : CommonFormats.HighlightYellow(actionText);

      row.Q2<Label>("Condition").text = _uiFactory.T(ConditionTextLocKey, conditionText);
      row.Q2<Label>("Action").text = _uiFactory.T(ActionTextLocKey, actionText);
      row.Q2<VisualElement>("Container").SetEnabled(action.Condition.IsActive);
      _rulesList.Add(row);
    }
  }

  string ShortenNames(string description) {
    return _scriptingService.GetScriptableNames()
        .Aggregate(description, (current, scriptableName) => current.Replace(" " + scriptableName + ".", " "));
  }

  void SetRulesListFolded(bool isFolded) {
    _rulesListFolded = isFolded;
    var actualFoldState = isFolded || _scriptingRulesUIHelper.RulesCount == 0;
    _rulesList.ToggleDisplayStyle(!actualFoldState);
    _foldRulesButton.ToggleDisplayStyle(!actualFoldState);
    _unfoldRulesButton.ToggleDisplayStyle(actualFoldState);
    _unfoldRulesButton.SetEnabled(_scriptingRulesUIHelper.RulesCount > 0);
  }

  void SetSignalsListFolded(bool isFolded) {
    _signalsListFolded = isFolded;
    var actualFoldState = _signalsListFolded || _scriptingRulesUIHelper.ExposedSignalsCount == 0;
    _signalsList.ToggleDisplayStyle(!actualFoldState);
    _foldSignalsButton.ToggleDisplayStyle(!actualFoldState);
    _unfoldSignalsButton.ToggleDisplayStyle(actualFoldState);
    _unfoldSignalsButton.SetEnabled(_scriptingRulesUIHelper.ExposedSignalsCount > 0);
  }

  void ClearAllRules() {
    var i = 0;
    while (i < _automationBehavior.Actions.Count) {
      if (!ScriptingRulesUIHelper.IsSignalMapping(_automationBehavior.Actions[i])) {
        _automationBehavior.DeleteRuleAt(0);
      } else {
        i++;
      }
    }
    UpdateFragment();
  }

  void ClearAllSignals() {
    var i = 0;
    while (i < _automationBehavior.Actions.Count) {
      if (ScriptingRulesUIHelper.IsSignalMapping(_automationBehavior.Actions[i])) {
        _automationBehavior.DeleteRuleAt(0);
      } else {
        i++;
      }
    }
    UpdateFragment();
  }
}
