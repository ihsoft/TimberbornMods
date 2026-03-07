// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using Timberborn.Common;
using Timberborn.CoreUI;
using Timberborn.TooltipSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RuleRow {

  const string ConditionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ConditionLabel";
  const string ActionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ActionLabel";
  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";
  const string TemplateFamilyLocKey = "IgorZ.Automation.Scripting.Editor.TemplateFamilyLabel";
  const string DeleteRuleBtnLocKey = "IgorZ.Automation.Scripting.Editor.DeleteRuleBtn";
  const string PauseRuleBtnLocKey = "IgorZ.Automation.Scripting.Editor.PauseRuleBtn";
  const string ResetChangesBtnLocKey = "IgorZ.Automation.Scripting.Editor.ResetChangesBtn";
  const string ResumeRuleBtnLocKey = "IgorZ.Automation.Scripting.Editor.ResumeRuleBtn";

  const string EditModeStyle = "editmode-rule";
  const string OriginalRuleStyle = "original-rule";
  const string ModifiedRuleStyle = "modified-rule";
  const string DeletedTextStyle = "automation-red-text";

  #region API

  public readonly VisualElement Root;

  public BooleanOperator ParsedCondition { get; private set; }
  public string ConditionExpression {
    get => _conditionExpression;
    set {
      if (LegacyAction != null) {
        throw new InvalidOperationException("Cannot set condition expression for legacy action.");
      }
      _conditionExpression = value;
      ParsedCondition = _parserFactory.ParseCondition(value, ActiveBuilding, out _);
      CheckIfModified();
    }
  }
  string _conditionExpression;

  public ActionOperator ParsedAction { get; private set; }
  public string ActionExpression {
    get => _actionExpression;
    set {
      if (LegacyAction != null) {
        throw new InvalidOperationException("Cannot set action expression for legacy action.");
      }
      _actionExpression = value;
      ParsedAction = _parserFactory.ParseAction(value, ActiveBuilding, out _);
      CheckIfModified();
    }
  }
  string _actionExpression;

  public IAutomationAction LegacyAction { get; private set; }

  public AutomationBehavior ActiveBuilding { get; private set; }

  public bool IsNew => _originalConditionExpression == null && _originalActionExpression == null;

  public bool IsModified {
    get => _isModified;
    private set {
      _isModified = value;
      SetContainerClass();
    }
  }
  bool _isModified;

  public bool IsInEditMode {
    get => _isInEditMode;
    private set {
      _isInEditMode = value;
      SetContainerClass();
    }
  }
  bool _isInEditMode;

  public bool IsDeleted {
    get => _isDeleted;
    private set {
      _isDeleted = value;
      _deletedStateOverlay.ToggleDisplayStyle(value);
      _ruleContainer.ToggleDisplayStyle(!value);
    }
  }
  bool _isDeleted;

  public bool IsEnabled {
    get => _isEnabled;
    set {
      _isEnabled = value;
      _pauseRuleBtn.ToggleDisplayStyle(!IsInEditMode && _isEnabled);
      _resumeRuleBtn.ToggleDisplayStyle(!IsInEditMode && !_isEnabled);
      CheckIfModified();
    }
  }
  bool _isEnabled = true;

  public bool HasErrors => (ParsedAction == null || ParsedCondition == null) && !IsDeleted && !IsInEditMode;

  public event EventHandler OnStateChanged;

  public RulesEditorDialog RulesEditorDialog { get; private set; }

  /// <summary>The main factory method. Don't create via injection.</summary>
  public static RuleRow CreateFor(RulesEditorDialog rulesEditorDialog, AutomationBehavior automationBehavior) {
    var row = StaticBindings.DependencyContainer.GetInstance<RuleRow>();
    row.RulesEditorDialog = rulesEditorDialog;
    row.ActiveBuilding = automationBehavior;
    return row;
  }

  public void Initialize(ScriptedCondition condition, ScriptedAction action) {
    _originalConditionExpression = condition.ParsingResult.ParsedExpression != null
        ? _parserFactory.DefaultParser.Decompile(condition.ParsingResult.ParsedExpression)
        : ParserFactory.LispSyntaxPrefix + condition.Expression;
    ConditionExpression = _originalConditionExpression;

    _originalActionExpression = action.ParsingResult.ParsedExpression != null
        ? _parserFactory.DefaultParser.Decompile(action.ParsingResult.ParsedExpression)
        : ParserFactory.LispSyntaxPrefix + action.Expression;
    ActionExpression = _originalActionExpression;

    _originalTemplateFamily = action.TemplateFamily;
    SetTemplateFamily(_originalTemplateFamily);
    _originalEnabledState = condition.IsEnabled;
    IsEnabled = _originalEnabledState;
  }

  public void Initialize(IAutomationAction legacyAction) {
    LegacyAction = legacyAction;
    _originalTemplateFamily = legacyAction.TemplateFamily;
    SetTemplateFamily(_originalTemplateFamily);
    _originalEnabledState = true;
    IsEnabled = _originalEnabledState;
  }

  public IAutomationCondition GetCondition() {
    if (IsDeleted) {
      throw new InvalidOperationException("Cannot get condition if deleted.");
    }
    if (LegacyAction != null) {
      return LegacyAction.Condition.CloneDefinition();
    }
    var condition = new ScriptedCondition();
    condition.SetEnabled(IsEnabled);
    condition.SetExpression(_parserFactory.LispSyntaxParser.Decompile(ParsedCondition));
    return condition;
  }

  public IAutomationAction GetAction() {
    if (IsDeleted) {
      throw new InvalidOperationException("Cannot get action if deleted.");
    }
    if (LegacyAction != null) {
      return LegacyAction.CloneDefinition();
    }
    var action = new ScriptedAction();
    action.SetExpression(_parserFactory.LispSyntaxParser.Decompile(ParsedAction));
    action.TemplateFamily = _templateFamily;
    return action;
  }

  public void CreateEditView(VisualElement editorRoot) {
    Reset();
    _editView.Add(editorRoot);
    _editView.ToggleDisplayStyle(true);
    IsInEditMode = true;
    OnStateChanged?.Invoke(this, EventArgs.Empty);
  }

  public void SwitchToViewMode() {
    Reset();

    // Rule content.
    GetDescriptions(out var conditionDesc, out var actionDesc);
    var ruleTextLabel = _readOnlyView.Q<Label>();
    ruleTextLabel.text =
        _uiFactory.T(ConditionLabelLocKey) + " " + conditionDesc
        + "\n" + _uiFactory.T(ActionLabelLocKey) + " " + actionDesc;
    ruleTextLabel.SetEnabled(IsEnabled);
    _readOnlyView.ToggleDisplayStyle(true);

    // Controls.
    _revertChangesBtn.ToggleDisplayStyle(IsModified && !IsNew);
    var ruleRowButtonProviders =
        _editorProviders.Where(provider => provider.RuleRowBtnLocKey != null && provider.IsRuleRowBtnEnabled(this));
    foreach (var provider in ruleRowButtonProviders) {
      CreateButton(provider.RuleRowBtnLocKey, _ => provider.OnRuleRowBtnAction(this));
    }

    IsInEditMode = false;
    IsEnabled = IsEnabled;  // Refresh buttons state. It depends on the edit mode.
    OnStateChanged?.Invoke(this, EventArgs.Empty);
  }

  public void DiscardChangesAndSwitchToViewMode() {
    if (IsNew && !IsModified) {
      MarkDeletedAction();
    }
    SwitchToViewMode();
  }

  void CreateButton(string locKey, Action<Button> onClick, bool addAtBeginning = false) {
    var button = _uiFactory.LoadVisualElement<Button>("IgorZ.Automation/RuleRowButtonTmpl");
    button.text = _uiFactory.T(locKey);
    button.clicked += () => onClick(button);
    if (addAtBeginning) {
      _ruleButtons.Insert(0, button);
    } else {
      _ruleButtons.Add(button);
    }
  }

  public void ReportError(string error) {
    _notifications.Q<Label>("ErrorText").text = error;
    _notifications.ToggleDisplayStyle(true);
  }

  public void ReportError(ScriptError error) {
    var errorMessage = error.Message;
    if (error is ScriptError.RuntimeError runtimeError) {
      errorMessage = _uiFactory.T(runtimeError.LocKey) + "\n" + errorMessage;
    } else if (error is ScriptError.LocParsingError locParsingError){
      errorMessage = _uiFactory.T(locParsingError.LocKey);
    }
    _notifications.Q<Label>("ErrorText").text = errorMessage;
    _notifications.ToggleDisplayStyle(true);
  }

  public void ClearError() {
    _notifications.ToggleDisplayStyle(false);
  }

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  readonly ExpressionDescriber _expressionDescriber;
  readonly ParserFactory _parserFactory;
  readonly ImmutableArray<IEditorButtonProvider> _editorProviders;

  readonly VisualElement _ruleButtons;
  readonly VisualElement _notifications;
  readonly VisualElement _readOnlyView;
  readonly VisualElement _editView;
  readonly VisualElement _templateFamilySection;
  readonly VisualElement _ruleContainer;
  readonly VisualElement _deletedStateOverlay;

  readonly Button _revertChangesBtn;
  readonly Button _pauseRuleBtn;
  readonly Button _resumeRuleBtn;

  string _originalConditionExpression;
  string _originalActionExpression;
  string _templateFamily;
  string _originalTemplateFamily;
  bool _originalEnabledState;

  RuleRow(UiFactory uiFactory, ExpressionDescriber expressionDescriber, ParserFactory parserFactory,
          ITooltipRegistrar tooltipRegistrar, IEnumerable<IEditorButtonProvider> editors) {
    _uiFactory = uiFactory;
    _expressionDescriber = expressionDescriber;
    _parserFactory = parserFactory;
    _editorProviders = editors.Where(x => x.RuleRowBtnLocKey != null).ToImmutableArray();

    Root = _uiFactory.LoadVisualTreeAsset("IgorZ.Automation/RuleRow");

    _revertChangesBtn = Root.Q<Button>("RevertChangesBtn");
    _revertChangesBtn.clicked += RevertChangesAction;
    tooltipRegistrar.RegisterLocalizable(_revertChangesBtn, ResetChangesBtnLocKey);

    var moveRuleUpBtn = Root.Q<Button>("MoveRuleUpBtn");
    moveRuleUpBtn.clicked += MoveRuleUpAction;
    var moveRuleDownBtn = Root.Q<Button>("MoveRuleDownBtn");
    moveRuleDownBtn.clicked += MoveRuleDownAction;

    var deleteRuleBtn = Root.Q<Button>("DeleteRuleBtn");
    tooltipRegistrar.RegisterLocalizable(deleteRuleBtn, DeleteRuleBtnLocKey);
    deleteRuleBtn.clicked += MarkDeletedAction;

    _pauseRuleBtn = Root.Q<Button>("PauseRuleBtn");
    tooltipRegistrar.RegisterLocalizable(_pauseRuleBtn, PauseRuleBtnLocKey);
    _pauseRuleBtn.clicked += () => { IsEnabled = false; };
    _resumeRuleBtn = Root.Q<Button>("ResumeRuleBtn");
    tooltipRegistrar.RegisterLocalizable(_resumeRuleBtn, ResumeRuleBtnLocKey);
    _resumeRuleBtn.clicked += () => { IsEnabled = true; };

    _ruleButtons = Root.Q("RuleButtons");
    _readOnlyView = Root.Q("ReadOnlyRuleView");
    _editView = Root.Q("EditRuleView");
    _notifications  = Root.Q("Notifications");
    _notifications.ToggleDisplayStyle(false);
    _templateFamilySection = Root.Q("TemplateFamilySection");
    _templateFamilySection.ToggleDisplayStyle(false);
    _templateFamilySection.Q<Button>("RemoveTemplateBtn").clicked += () => SetTemplateFamily(null);
    _templateFamilySection.Q<Button>("RevertTemplateBtn").clicked += () => SetTemplateFamily(_originalTemplateFamily);
    _ruleContainer = Root.Q("RuleContainer");
    _deletedStateOverlay = Root.Q("DeletedStateOverlay");
    _deletedStateOverlay.ToggleDisplayStyle(false);
    _deletedStateOverlay.Q<Button>("UndoDeleteBtn").clicked += () => { IsDeleted = false; };
  }

  void Reset() {
    _editView.Clear();
    _editView.ToggleDisplayStyle(false);
    _readOnlyView.ToggleDisplayStyle(false);
    _ruleButtons.Clear();
    _notifications.ToggleDisplayStyle(false);
    _revertChangesBtn.ToggleDisplayStyle(false);
  }

  void SetContainerClass() {
    var ruleContainer = Root.Q("RuleContainer");
    if (IsInEditMode) {
      ruleContainer.EnableInClassList(EditModeStyle, true);
      ruleContainer.EnableInClassList(OriginalRuleStyle, false);
      ruleContainer.EnableInClassList(ModifiedRuleStyle, false);
    } else if (IsModified) {
      ruleContainer.EnableInClassList(EditModeStyle, false);
      ruleContainer.EnableInClassList(OriginalRuleStyle, false);
      ruleContainer.EnableInClassList(ModifiedRuleStyle, true);
    } else {
      ruleContainer.EnableInClassList(EditModeStyle, false);
      ruleContainer.EnableInClassList(OriginalRuleStyle, true);
      ruleContainer.EnableInClassList(ModifiedRuleStyle, false);
    }
  }

  void CheckIfModified() {
    IsModified = _originalConditionExpression != _conditionExpression
            || _originalActionExpression != _actionExpression
            || _originalTemplateFamily != _templateFamily
            || _originalEnabledState != IsEnabled;
  }

  void GetDescriptions(out string condition, out string action) {
    if (LegacyAction is not null) {
      condition = LegacyAction.Condition.UiDescription;
      action = LegacyAction.UiDescription;
      return;
    }
    condition = GetDescription(ParsedCondition);
    action = GetDescription(ParsedAction);
  }

  string GetDescription(IExpression expression) {
    if (expression == null) {
      return CommonFormats.HighlightRed(_uiFactory.T(ParseErrorLocKey));
    }
    try {
      var description = _expressionDescriber.DescribeExpression(expression);
      if (IsEnabled && expression is BooleanOperator boolOperator && boolOperator.Execute()) {
        return CommonFormats.HighlightGreen(description);
      }
      return CommonFormats.HighlightYellow(description);
    } catch (ScriptError.RuntimeError e) {
      DebugEx.Error("Failed to get description: {0}\n{1}", expression, e);
      return CommonFormats.HighlightRed(_uiFactory.T(e.LocKey));
    }
  }

  void RevertChangesAction() {
    ConditionExpression = _originalConditionExpression;
    ActionExpression = _originalActionExpression;
    IsEnabled = _originalEnabledState;
    SwitchToViewMode();
  }

  void MarkDeletedAction() {
    IsDeleted = true;
    OnStateChanged?.Invoke(this, EventArgs.Empty);
  }

  void MoveRuleUpAction() {
    var rulePos = RulesEditorDialog.RuleRows.IndexOf(this);
    if (rulePos != 0) {
      RulesEditorDialog.SwapRows(rulePos, rulePos - 1);
      RulesEditorDialog.ContentScrollView.ScrollTo(Root);
    }
  }

  void MoveRuleDownAction() {
    var rulePos = RulesEditorDialog.RuleRows.IndexOf(this);
    if (rulePos != RulesEditorDialog.RuleRows.Count - 1) {
      RulesEditorDialog.SwapRows(rulePos, rulePos + 1);
      RulesEditorDialog.ContentScrollView.ScrollTo(Root);
    }
  }

  void SetTemplateFamily(string templateFamily) {
    _templateFamily = templateFamily;
    CheckIfModified();
    if (templateFamily != null && _originalTemplateFamily != null && templateFamily != _originalTemplateFamily) {
      // Editing the template family is not supported yet.
      throw new NotImplementedException("Editing the template family is not supported yet.");
    }
    if (_originalTemplateFamily == null && templateFamily == null) {
      _templateFamilySection.ToggleDisplayStyle(false);
      return;
    }
    _templateFamilySection.ToggleDisplayStyle(true);
    var label = _templateFamilySection.Q<Label>("TemplateFamilyName");
    if (_originalTemplateFamily != templateFamily) {
      label.AddToClassList(DeletedTextStyle);
      label.text = CommonFormats.Strikethrough(_uiFactory.T(TemplateFamilyLocKey, _originalTemplateFamily));
      _templateFamilySection.Q("RemoveTemplateBtn").ToggleDisplayStyle(false);
      _templateFamilySection.Q("RevertTemplateBtn").ToggleDisplayStyle(true);
    } else {
      label.RemoveFromClassList(DeletedTextStyle);
      label.text = _uiFactory.T(TemplateFamilyLocKey, _originalTemplateFamily);
      _templateFamilySection.Q("RemoveTemplateBtn").ToggleDisplayStyle(true);
      _templateFamilySection.Q("RevertTemplateBtn").ToggleDisplayStyle(false);
    }
  }

  #endregion
}
