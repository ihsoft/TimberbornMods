// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.Automation.ScriptingEngineUI;
using IgorZ.Automation.Settings;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.InventorySystem;
using Timberborn.TooltipSystem;
using UnityEngine.UIElements;

namespace IgorZ.Automation.AutomationSystemUI;

sealed class AutomationFragment : IEntityPanelFragment, ISignalListener {

  const string RuleRowTemplate = "IgorZ.Automation/FragmentRule";
  const string SignalRowTemplate = "IgorZ.Automation/FragmentSignal";
  const string FragmentResource = "IgorZ.Automation/EntityPanelFragment";

  const string ExportRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.ExportRulesBtnHint";
  const string ImportRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.ImportRulesBtnHint";

  const string RulesCountTextLocKey = "IgorZ.Automation.AutomationFragment.RulesCountText";
  const string ConditionTextLocKey = "IgorZ.Automation.AutomationFragment.RuleConditionText";
  const string ActionTextLocKey = "IgorZ.Automation.AutomationFragment.RuleActionText";
  const string AndMoreRowLocKey = "IgorZ.Automation.AutomationFragment.AndMoreRow";
  const string SetupRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.SetupRulesBtnHint";
  const string CopyRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.CopyRulesBtnHint";
  const string ClearRulesBtnHintLocKey = "IgorZ.Automation.AutomationFragment.ClearRulesBtnHint";

  const string SignalsCountTextLocKey = "IgorZ.Automation.AutomationFragment.SignalsCountText";
  const string SetupSignalsBtnHintLocKey = "IgorZ.Automation.AutomationFragment.SetupSignalsBtnHint";
  const string CopySignalsBtnHintLocKey = "IgorZ.Automation.AutomationFragment.CopySignalsBtnHint";
  const string ClearSignalsBtnHintLocKey = "IgorZ.Automation.AutomationFragment.ClearSignalsBtnHint";

  readonly UiFactory _uiFactory;
  readonly CopyRulesTool _copyRulesTool;
  readonly ScriptingService _scriptingService;
  readonly RulesUIHelper _rulesHelper;
  readonly ITooltipRegistrar _tooltipRegistrar;

  VisualElement _root;

  VisualElement _rulesList;
  bool _rulesListFolded = true;
  Label _rulesCountLabel;
  Button _foldRulesButton;
  Button _unfoldRulesButton;
  Button _copyRulesButton;
  Button _clearRulesButton;

  VisualElement _signalsList;
  bool _signalsListFolded = true;
  Label _signalsCountLabel;
  Button _foldSignalsButton;
  Button _unfoldSignalsButton;
  Button _clearSignalsButton;
  Button _copySignalsButton;

  AutomationBehavior _automationBehavior;
  IGoodDisallower _goodDisallower;
  int _automationBehaviorVersion = -1;
  readonly List<SignalOperator> _signalConditionExpressions = [];

  AutomationFragment(UiFactory uiFactory, CopyRulesTool copyRulesTool, ScriptingService scriptingService,
                     RulesUIHelper rulesHelper, ITooltipRegistrar tooltipRegistrar) {
    _uiFactory = uiFactory;
    _copyRulesTool = copyRulesTool;
    _scriptingService = scriptingService;
    _rulesHelper = rulesHelper;
    _tooltipRegistrar = tooltipRegistrar;
  }

  #region ISignalListener implementation

  /// <inheritdoc/>
  public AutomationBehavior Behavior => _automationBehavior;

  /// <inheritdoc/>
  public void OnValueChanged(string signalName) => _automationBehaviorVersion = -1;

  #endregion

  public VisualElement InitializeFragment() {
    _root = _uiFactory.LoadVisualTreeAsset(FragmentResource);
    _rulesList = _root.Q2<VisualElement>("RulesList");
    _signalsList = _root.Q2<VisualElement>("SignalsList");

    // Import/Export fragment section.
    var importButton = _root.Q2<Button>("ImportRulesButton");
    importButton.clicked += () => {
      var importRulesDialog = StaticBindings.DependencyContainer.GetInstance<ImportRulesDialog>();
      importRulesDialog.WithBuilding(_automationBehavior).Show();
    };
    _tooltipRegistrar.RegisterLocalizable(importButton, ImportRulesBtnHintLocKey);
    var exportButton = _root.Q2<Button>("ExportRulesButton");
    exportButton.clicked += () => {
      var exportRulesDialog = StaticBindings.DependencyContainer.GetInstance<ExportRulesDialog>(); 
      exportRulesDialog.WithActions(_automationBehavior.Actions).Show();
    };
    _tooltipRegistrar.RegisterLocalizable(exportButton, ExportRulesBtnHintLocKey);

    // Setup signals fragment section.
    var setupSignalsButton = _root.Q2<Button>("SetupSignalsButton");
    setupSignalsButton.clicked += () => {
      var signalsEditorDialog = StaticBindings.DependencyContainer.GetInstance<SignalsEditorDialog>();
      signalsEditorDialog.WithUiHelper(_rulesHelper).Show();
    };
    _tooltipRegistrar.RegisterLocalizable(setupSignalsButton, SetupSignalsBtnHintLocKey);
    _copySignalsButton = _root.Q2<Button>("CopySignalsButton");
    _copySignalsButton.clicked += () => _copyRulesTool.StartTool(_rulesHelper, CopyRulesTool.CopyMode.CopySignals);
    _tooltipRegistrar.RegisterLocalizable(_copySignalsButton, CopySignalsBtnHintLocKey);
    _clearSignalsButton = _root.Q2<Button>("ClearSignalsButton");
    _tooltipRegistrar.RegisterLocalizable(_clearSignalsButton, ClearSignalsBtnHintLocKey);
    _clearSignalsButton.clicked += () => _rulesHelper.ClearSignalsOnBuilding();

    _signalsCountLabel = _root.Q2<Label>("SignalsCountLabel");
    _foldSignalsButton = _root.Q2<Button>("FoldSignalsButton");
    _unfoldSignalsButton = _root.Q2<Button>("UnfoldSignalsButton");
    _foldSignalsButton.clicked += () => SetSignalsListFolded(true);
    _unfoldSignalsButton.clicked += () => SetSignalsListFolded(false);

    // Setup rules fragment section.
    var setupRulesButton = _root.Q2<Button>("SetupRulesButton");
    setupRulesButton.clicked += () => {
      var rulesEditorDialog = StaticBindings.DependencyContainer.GetInstance<RulesEditorDialog>();
      rulesEditorDialog.WithUiHelper(_rulesHelper).Show();
    };
    _tooltipRegistrar.RegisterLocalizable(setupRulesButton, SetupRulesBtnHintLocKey);
    _copyRulesButton = _root.Q2<Button>("CopyRulesButton");
    _copyRulesButton.clicked += () => _copyRulesTool.StartTool(_rulesHelper, CopyRulesTool.CopyMode.CopyRules);
    _tooltipRegistrar.RegisterLocalizable(_copyRulesButton, CopyRulesBtnHintLocKey);
    _clearRulesButton = _root.Q2<Button>("ClearRulesButton");
    _tooltipRegistrar.RegisterLocalizable(_clearRulesButton, ClearRulesBtnHintLocKey);
    _clearRulesButton.clicked += () => _rulesHelper.ClearRulesOnBuilding();

    _rulesCountLabel = _root.Q2<Label>("RulesCountLabel");
    _foldRulesButton = _root.Q2<Button>("FoldRulesButton");
    _unfoldRulesButton = _root.Q2<Button>("UnfoldRulesButton");
    _foldRulesButton.clicked += () => SetRulesListFolded(true);
    _unfoldRulesButton.clicked += () => SetRulesListFolded(false);

    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _automationBehavior = entity.GetComponent<AutomationBehavior>();
    if (!_automationBehavior) {
      return;
    }
    _automationBehaviorVersion = -1;
    _goodDisallower = entity.GetComponent<IGoodDisallower>();
    if (_goodDisallower != null) {
      _goodDisallower.DisallowedGoodsChanged += OnGoodDisallowerChange;
    }
    _root.ToggleDisplayStyle(true);
  }

  public void ClearFragment() {
    _scriptingService.UnregisterSignals(_signalConditionExpressions, this);
    _signalConditionExpressions.Clear();
    _automationBehavior = null; // Must be after unregistering signals!
    if (_goodDisallower != null) {
      _goodDisallower.DisallowedGoodsChanged -= OnGoodDisallowerChange;
    }
    _root.ToggleDisplayStyle(visible: false);
    _rulesHelper.SetBuilding(null);
  }

  public void UpdateFragment() {
    if (!_automationBehavior || _automationBehavior.StateVersion == _automationBehaviorVersion) {
      return;
    }
    _automationBehaviorVersion = _automationBehavior.StateVersion;
    _rulesHelper.SetBuilding(_automationBehavior);
    SetRulesListFolded(_rulesListFolded);
    SetSignalsListFolded(_signalsListFolded);

    // Signals panel.
    var totalSignalsCount = _rulesHelper.BuildingSignalNames.Count;
    var exposedSignalsCount = _rulesHelper.ExposedSignalsCount;
    _root.Q2<VisualElement>("SignalsPanel").ToggleDisplayStyle(totalSignalsCount > 0 || exposedSignalsCount > 0);
    _signalsCountLabel.text = _uiFactory.T(SignalsCountTextLocKey, exposedSignalsCount, totalSignalsCount);
    _clearSignalsButton.SetEnabled(exposedSignalsCount > 0);
    _copySignalsButton.SetEnabled(exposedSignalsCount > 0);
    _signalsList.Clear();
    _scriptingService.UnregisterSignals(_signalConditionExpressions, this);
    _signalConditionExpressions.Clear();
    foreach (var signalMapping in _rulesHelper.BuildingSignals) {
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

      if (signalMapping.Action?.Condition is ScriptedCondition scriptedCondition) {
        _signalConditionExpressions.AddRange(
            _scriptingService.RegisterSignals(scriptedCondition.ParsingResult.ParsedExpression, this));
      }
    }

    // Rules panel.
    _rulesCountLabel.text = _uiFactory.T(RulesCountTextLocKey, _rulesHelper.BuildingRules.Count);
    _clearRulesButton.SetEnabled(_rulesHelper.BuildingRules.Count > 0);
    _copyRulesButton.SetEnabled(_rulesHelper.BuildingRules.Count > 0);
    _rulesList.Clear();
    var rowsAdded = 0;
    foreach (var action in _rulesHelper.BuildingRules) {
      var row = _uiFactory.LoadVisualElement(RuleRowTemplate);
      _rulesList.Add(row);
      rowsAdded++;

      var conditionLabel = row.Q2<Label>("Condition");
      var actionLabel = row.Q2<Label>("Action");

      if (rowsAdded >= EntityPanelSettings.MaxRulesShown && rowsAdded < _rulesHelper.BuildingRules.Count) {
        conditionLabel.text =
            _uiFactory.T(AndMoreRowLocKey, _rulesHelper.BuildingRules.Count - rowsAdded + 1);
        actionLabel.RemoveFromHierarchy();
        break;
      }
      row.Q2<VisualElement>("Container").SetEnabled(action.Condition.IsActive);

      // FIXME: make all descriptions from the UI helper.
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
    var actualFoldState = isFolded || _rulesHelper.BuildingRules.Count == 0;
    _rulesList.ToggleDisplayStyle(!actualFoldState);
    _foldRulesButton.ToggleDisplayStyle(!actualFoldState);
    _unfoldRulesButton.ToggleDisplayStyle(actualFoldState);
    _unfoldRulesButton.SetEnabled(_rulesHelper.BuildingRules.Count > 0);
  }

  void SetSignalsListFolded(bool isFolded) {
    _signalsListFolded = isFolded;
    var actualFoldState = _signalsListFolded || _rulesHelper.ExposedSignalsCount == 0;
    _signalsList.ToggleDisplayStyle(!actualFoldState);
    _foldSignalsButton.ToggleDisplayStyle(!actualFoldState);
    _unfoldSignalsButton.ToggleDisplayStyle(actualFoldState);
    _unfoldSignalsButton.SetEnabled(_rulesHelper.ExposedSignalsCount > 0);
  }

  void OnGoodDisallowerChange(object sender, DisallowedGoodsChangedEventArgs args) {
    _automationBehaviorVersion = -1;
  }
}
