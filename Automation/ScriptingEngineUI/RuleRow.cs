// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using TimberApi.DependencyContainerSystem;
using Timberborn.CoreUI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RuleRow {

  const string ConditionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ConditionLabel";
  const string ActionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ActionLabel";
  const string ResetChangesLocKey = "IgorZ.Automation.Scripting.Editor.ResetChangesBtn";
  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";
  const string TemplateFamilyLocKey = "IgorZ.Automation.Scripting.Editor.TemplateFamilyLabel";

  const string EditModeStyle = "editmode-rule";
  const string OriginalRuleStyle = "original-rule";
  const string ModifiedRuleStyle = "modified-rule";
  const string DeletedTextStyle = "automation-deleted-text";

  #region API

  public readonly VisualElement Root;

  public BoolOperator ParsedCondition { get; private set; }
  public string ConditionExpression {
    get => _conditionExpression;
    set {
      if (LegacyAction != null) {
        throw new InvalidOperationException("Cannot set condition expression for legacy action.");
      }
      _conditionExpression = value;
      ParsedCondition = ScriptedCondition.ParseAndValidate(value, ActiveBuilding, out _);
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
      ParsedAction = ScriptedAction.ParseAndValidate(value, ActiveBuilding, out _);
      CheckIfModified();
    }
  }
  string _actionExpression;

  public IAutomationAction LegacyAction { get; private set; }

  public readonly AutomationBehavior ActiveBuilding;

  public bool IsNew => _originalCondition == null && _originalAction == null;

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

  public bool HasErrors => (ParsedAction == null || ParsedCondition == null) && !IsDeleted && !IsInEditMode;

  public event EventHandler OnStateChanged;

  public RuleRow(IEnumerable<IEditorProvider> editors, UiFactory uiFactory, AutomationBehavior activeBuilding) {
    _expressionParser = DependencyContainer.GetInstance<ExpressionParser>();
    _uiFactory = uiFactory;
    ActiveBuilding = activeBuilding;
    _editorProviders = editors.ToArray();

    Root = _uiFactory.LoadVisualTreeAsset("IgorZ.Automation/RuleRow");
    _sidePanel = Root.Q("SidePanel");
    _sidePanel.Q<Button>("DeleteRowBtn").clicked += MarkDeleted;
    _bottomRowSection = Root.Q("BottomRowSection");
    _ruleButtons = Root.Q("RuleButtons");
    _readonlyView = Root.Q("ReadonlyRuleView");
    _editView = Root.Q("EditRuleView");
    _notifications  = Root.Q("Notifications");
    _notifications.ToggleDisplayStyle(false);
    _templateFamilySection = Root.Q("TemplateFamilySection");
    _templateFamilySection.ToggleDisplayStyle(false);
    _templateFamilySection.Q<Button>("RemoveTemplateBtn").clicked += () => {
      SetTemplateFamily(null);
    };
    _templateFamilySection.Q<Button>("RevertTemplateBtn").clicked += () => {
      SetTemplateFamily(_originalTemplateFamily);
    };
    _ruleContainer = Root.Q("RuleContainer");
    _deletedStateOverlay = Root.Q("DeletedStateOverlay");
    _deletedStateOverlay.ToggleDisplayStyle(false);
    _deletedStateOverlay.Q<Button>("UndoDeleteBtn").clicked += () => {
      IsDeleted = false;
    };
  }

  public void Initialize(ScriptedCondition condition, ScriptedAction action) {
    _originalCondition = (ScriptedCondition) condition?.CloneDefinition();
    ConditionExpression = _originalCondition != null ? _originalCondition.Expression : "";
    _originalAction = (ScriptedAction) action?.CloneDefinition();
    ActionExpression = _originalAction != null ? _originalAction.Expression : "";
    _originalTemplateFamily = _originalAction?.TemplateFamily;
    SetTemplateFamily(_originalTemplateFamily);
  }

  public void Initialize(IAutomationAction legacyAction) {
    LegacyAction = legacyAction;
    _originalTemplateFamily = legacyAction.TemplateFamily;
    SetTemplateFamily(_originalTemplateFamily);
  }

  public IAutomationCondition GetCondition() {
    if (IsDeleted) {
      throw new InvalidOperationException("Cannot get condition if deleted.");
    }
    if (LegacyAction != null) {
      return LegacyAction.Condition.CloneDefinition();
    }
    var condition = _originalCondition != null
        ? (ScriptedCondition)_originalCondition.CloneDefinition()
        : new ScriptedCondition();
    condition.SetExpression(ConditionExpression);
    return condition;
  }

  public IAutomationAction GetAction() {
    if (IsDeleted) {
      throw new InvalidOperationException("Cannot get action if deleted.");
    }
    if (LegacyAction != null) {
      return LegacyAction.CloneDefinition();
    }
    var action = _originalAction != null
        ? (ScriptedAction)_originalAction.CloneDefinition()
        : new ScriptedAction();
    action.SetExpression(ActionExpression);
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

    // Side panel
    _sidePanel.ToggleDisplayStyle(true);

    // Rule content.
    GetDescriptions(out var conditionDesc, out var actionDesc);
    _readonlyView.Q<Label>("ReadonlyRuleView").text =
        _uiFactory.T(ConditionLabelLocKey) + " " + conditionDesc
        + "\n" + _uiFactory.T(ActionLabelLocKey) + " " + actionDesc;
    _readonlyView.ToggleDisplayStyle(true);

    // Controls.
    if (IsModified && !IsNew) {
      CreateButton(ResetChangesLocKey, _ => {
        ConditionExpression = _originalCondition.Expression;
        ActionExpression = _originalAction.Expression;
        SwitchToViewMode();
      });
    }
    var ruleEditable = false;
    foreach (var provider in _editorProviders) {
      var canEdit = provider.VerifyIfEditable(this);
      if (!canEdit) {
        continue;
      }
      CreateButton(provider.EditRuleLocKey, _ => provider.MakeForRule(this));
      ruleEditable = true;
    }
    Root.Q("BottomRowSection").ToggleDisplayStyle(ruleEditable || _originalTemplateFamily != null);

    IsInEditMode = false;
    OnStateChanged?.Invoke(this, EventArgs.Empty);
  }

  public void DiscardChangesAndSwitchToViewMode() {
    if (IsNew && !IsModified) {
      MarkDeleted();
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
  readonly IEditorProvider[] _editorProviders;
  readonly ExpressionParser _expressionParser;

  readonly VisualElement _sidePanel;
  readonly VisualElement _ruleButtons;
  readonly VisualElement _bottomRowSection;
  readonly VisualElement _notifications;
  readonly VisualElement _readonlyView;
  readonly VisualElement _editView;
  readonly VisualElement _templateFamilySection;
  readonly VisualElement _ruleContainer;
  readonly VisualElement _deletedStateOverlay;

  ScriptedCondition _originalCondition;
  ScriptedAction _originalAction;
  string _templateFamily;
  string _originalTemplateFamily;

  void Reset() {
    _editView.Clear();
    _editView.ToggleDisplayStyle(false);
    _readonlyView.ToggleDisplayStyle(false);
    _sidePanel.ToggleDisplayStyle(false);
    _ruleButtons.Clear();
    _bottomRowSection.ToggleDisplayStyle(false);
    _notifications.ToggleDisplayStyle(false);
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
    IsModified = _originalCondition?.Expression != _conditionExpression
            || _originalAction?.Expression != _actionExpression
            || _originalTemplateFamily != _templateFamily;
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
    var description = _expressionParser.GetDescription(expression, logErrors: true);
    var isGreen = false;
    if (expression is BoolOperator boolOperator) {
      try {
        isGreen = boolOperator.Execute();
      } catch (ScriptError.RuntimeError e) {
        DebugEx.Error("Failed to execute condition: {0}\n{1}", expression, e);
        return CommonFormats.HighlightRed(_uiFactory.T(e.LocKey));
      }
    }
    return isGreen ? CommonFormats.HighlightGreen(description) : CommonFormats.HighlightYellow(description);
  }

  void MarkDeleted() {
    IsDeleted = true;
    OnStateChanged?.Invoke(this, EventArgs.Empty);
  }

  void SetTemplateFamily(string templateFamily) {
    _templateFamily = templateFamily;
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
