// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using Timberborn.Localization;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RuleRow {

  const string ConditionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ConditionLabel";
  const string ActionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ActionLabel";
  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";
  const string ResetChangesLocKey = "IgorZ.Automation.Scripting.Editor.ResetChangesBtn";

  #region API

  public readonly VisualElement Root;

  public BoolOperatorExpr ParsedCondition { get; private set; }
  public string ConditionExpression {
    get => _conditionExpression;
    set {
      if (LegacyAction != null) {
        throw new InvalidOperationException("Cannot set condition expression for legacy action.");
      }
      _conditionExpression = value;
      ParsedCondition = ParseExpression<BoolOperatorExpr>(value);
      CheckIfModified();
    }
  }
  string _conditionExpression;
  string _originalConditionExpression;

  public ActionExpr ParsedAction { get; private set; }
  public string ActionExpression {
    get => _actionExpression;
    set {
      if (LegacyAction != null) {
        throw new InvalidOperationException("Cannot set action expression for legacy action.");
      }
      _actionExpression = value;
      ParsedAction = ParseExpression<ActionExpr>(value);
      CheckIfModified();
    }
  }
  string _actionExpression;
  string _originalActionExpression;

  public IAutomationAction LegacyAction {
    get => _legacyAction;
    private set {
      if (_originalConditionExpression != null || _originalActionExpression != null) {
        throw new InvalidOperationException(
            "Cannot set legacy action if it already has expressions.");
      }
      _legacyAction = value;
    }
  }
  IAutomationAction _legacyAction;

  public readonly AutomationBehavior ActiveBuilding;

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

  public bool IsDeleted { get; private set; }

  public event EventHandler OnStateChanged;

  public RuleRow(IEnumerable<IEditorProvider> editors, UiFactory uiFactory, AutomationBehavior activeBuilding) {
    _uiFactory = uiFactory;
    ActiveBuilding = activeBuilding;
    _editorProviders = editors.ToArray();

    Root = _uiFactory.LoadVisualTreeAsset("IgorZ.Automation/RuleRow");
    _sidePanel = Root.Q("SidePanel");
    _sidePanel.Q<Button>("DeleteRowBtn").clicked += MarkDeleted;
    _ruleButtons = Root.Q("RuleButtons");
    _readonlyView = Root.Q("ReadonlyRuleView");
    _editView = Root.Q("EditRuleView");
    _notifications  = Root.Q("Notifications");
    _notifications.ToggleDisplayStyle(false);
  }

  public void Initialize(string condition, string action) {
    _originalConditionExpression = condition;
    ConditionExpression = condition;
    _originalActionExpression = action;
    ActionExpression = action;
  }

  public void Initialize(IAutomationAction legacyAction) {
    LegacyAction = legacyAction;
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
    if (IsModified) {
      CreateButton(ResetChangesLocKey, _ => {
        ConditionExpression = _originalConditionExpression;
        ActionExpression = _originalActionExpression;
        SwitchToViewMode();
      });
    }
    var ruleEditable = false;
    foreach (var provider in _editorProviders) {
      var canEdit = provider.VerifyIfEditable(this, ActiveBuilding);
      if (!canEdit) {
        continue;
      }
      CreateButton(provider.EditRuleLocKey, _ => provider.MakeForRule(this));
      ruleEditable = true;
    }
    _ruleButtons.ToggleDisplayStyle(ruleEditable);

    IsInEditMode = false;
    OnStateChanged?.Invoke(this, EventArgs.Empty);
  }

  public void DiscardChangesAndSwitchToViewMode() {
    if (IsNew) {
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

  public void ClearError() {
    _notifications.ToggleDisplayStyle(false);
  }

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  readonly IEditorProvider[] _editorProviders;

  readonly VisualElement _sidePanel;
  readonly VisualElement _ruleButtons;
  readonly VisualElement _notifications;
  readonly VisualElement _readonlyView;
  readonly VisualElement _editView;


  void Reset() {
    _editView.Clear();
    _editView.ToggleDisplayStyle(false);
    _readonlyView.ToggleDisplayStyle(false);
    _sidePanel.ToggleDisplayStyle(false);
    _ruleButtons.Clear();
    _ruleButtons.ToggleDisplayStyle(false);
    _notifications.ToggleDisplayStyle(false);
  }

  void SetContainerClass() {
    var ruleContainer = Root.Q("RuleContainer");
    if (IsInEditMode) {
      ruleContainer.EnableInClassList("editmode-rule", true);
      ruleContainer.EnableInClassList("original-rule", false);
      ruleContainer.EnableInClassList("modified-rule", false);
    } else if (IsModified) {
      ruleContainer.EnableInClassList("editmode-rule", false);
      ruleContainer.EnableInClassList("original-rule", false);
      ruleContainer.EnableInClassList("modified-rule", true);
    } else {
      ruleContainer.EnableInClassList("editmode-rule", false);
      ruleContainer.EnableInClassList("original-rule", true);
      ruleContainer.EnableInClassList("modified-rule", false);
    }
  }

  void CheckIfModified() {
    IsModified = _originalConditionExpression != _conditionExpression || _originalActionExpression != _actionExpression;
  }

  T ParseExpression<T>(string expression) where T : class, IExpression {
    var parserContext = new ParserContext {
        ScriptHost = ActiveBuilding,
    };
    ExpressionParser.Instance.Parse(expression, parserContext);
    if (parserContext.LastError != null) {
      DebugEx.Warning("Failed to parse expression: {0}\nError: {1}",expression, parserContext.LastError);
      return null;
    }
    return parserContext.ParsedExpression as T;
  }

  void GetDescriptions(out string condition, out string action) {
    if (LegacyAction is not null) {
      condition = LegacyAction.Condition.UiDescription;
      action = LegacyAction.UiDescription;
      return;
    }

    if (ParsedCondition == null) {
      condition = _uiFactory.T(ParseErrorLocKey);
    } else {
      var context = new ParserContext() {
          ScriptHost = ActiveBuilding,
          ParsedExpression = ParsedCondition,
      };
      condition = ExpressionParser.Instance.GetDescription(context);
      condition = TextColors.ColorizeText($"<SolidHighlight>{condition}</SolidHighlight>");
      if (context.LastError != null) {
        DebugEx.Warning("Failed to get description for condition: {0}\nError: {1}",
                        _conditionExpression, context.LastError);
      }
    }

    if (ParsedAction == null) {
      action = _uiFactory.T(ParseErrorLocKey);
    } else {
      var context = new ParserContext() {
          ScriptHost = ActiveBuilding,
          ParsedExpression = ParsedAction,
      };
      action = ExpressionParser.Instance.GetDescription(context);
      action = TextColors.ColorizeText($"<SolidHighlight>{action}</SolidHighlight>");
      if (context.LastError != null) {
        DebugEx.Warning("Failed to get description for action: {0}\nError: {1}",
                        _actionExpression, context.LastError);
      }
    }
  }

  void MarkDeleted() {
    IsDeleted = true;
    OnStateChanged?.Invoke(this, EventArgs.Empty);
  }

  #endregion
}
