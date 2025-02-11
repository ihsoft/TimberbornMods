// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using TimberApi.UIBuilderSystem.StylingElements;
using TimberApi.UIPresets.Buttons;
using Timberborn.CoreUI;
using Timberborn.Localization;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RuleRow {

  const string ConditionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ConditionLabel";
  const string ActionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ActionLabel";
  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";

  const string SaveScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.SaveScriptBtn";
  const string DiscardScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.DiscardScriptBtn";

  static readonly Color BackgroundColor = new(1, 1, 1, 0.05f);
  static readonly Color ErrorTextColor = Color.red;

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
    }
  }
  string _conditionExpression;

  public ActionExpr ParsedAction { get; private set; }
  public string ActionExpression {
    get => _actionExpression;
    set {
      if (LegacyAction != null) {
        throw new InvalidOperationException("Cannot set action expression for legacy action.");
      }
      _actionExpression = value;
      ParsedAction = ParseExpression<ActionExpr>(value);
    }
  }
  string _actionExpression;

  public IAutomationAction LegacyAction { get; init; }

  public readonly AutomationBehavior ActiveBuilding;

  public bool IsInEditMode { get; private set; }
  public bool IsDeleted { get; private set; }

  public event EventHandler OnModeChanged;

  public RuleRow(IEnumerable<IEditorProvider> editors,
                 UiFactory uiFactory, AutomationBehavior activeBuilding) {
    _uiFactory = uiFactory;
    ActiveBuilding = activeBuilding;
    _editorProviders = editors.ToArray();
    MakeRoot(out Root, out _sidePanel, out _ruleContent, out _ruleButtons, out _notificationArea);
  }

  public void CreateEditView(VisualElement editorRoot, Func<bool> validateFn, Action saveFn) {
    Reset();
    CreateButton(SaveScriptBtnLocKey, _ => {
      if (validateFn?.Invoke() == false) {
        return;
      }
      saveFn?.Invoke();
      SwitchToViewMode();
    });
    CreateButton(DiscardScriptBtnLocKey, _ => {
      if (ConditionExpression == null && ActionExpression == null && LegacyAction == null) {
        MarkDeleted();
      } else {
        SwitchToViewMode();
      }
    });
    _ruleContent.Add(editorRoot);
    IsInEditMode = true;
    OnModeChanged?.Invoke(this, EventArgs.Empty);
  }

  public void SwitchToViewMode() {
    Reset();

    // Side panel
    var deleteBtn = _uiFactory.UiBuilder.Create<CrossButton>().BuildAndInitialize();
    deleteBtn.clicked += MarkDeleted;
    _sidePanel.Add(deleteBtn);

    // Rule content.
    var label = _uiFactory.CreateLabel();
    GetDescriptions(out var conditionDesc, out var actionDesc);
    label.text = _uiFactory.T(ConditionLabelLocKey) + " " + conditionDesc
        + "\n" + _uiFactory.T(ActionLabelLocKey) + " " + actionDesc;
    _ruleContent.Add(label);

    // Controls.
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
    OnModeChanged?.Invoke(this, EventArgs.Empty);
  }

  public void CreateButton(string locKey, Action<Button> onClick, bool addAtBeginning = false) {
    var button = _uiFactory.CreateButton(locKey, onClick, new Padding(0, 5, 0, 5));
    button.style.marginRight = 5;
    if (addAtBeginning) {
      _ruleButtons.Insert(0, button);
    } else {
      _ruleButtons.Add(button);
    }
  }

  public void ReportError(string error) {
    _notificationArea.Clear();
    var errorLabel = new Label();
    errorLabel.style.color = ErrorTextColor;
    errorLabel.text = error;
    _notificationArea.Add(errorLabel);
    _notificationArea.ToggleDisplayStyle(true);
  }

  public void ClearError() {
    _notificationArea.Clear();
    _notificationArea.ToggleDisplayStyle(false);
  }

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  readonly IEditorProvider[] _editorProviders;

  readonly VisualElement _sidePanel;
  readonly VisualElement _ruleContent;
  readonly VisualElement _ruleButtons;
  readonly VisualElement _notificationArea;

  void MakeRoot(out VisualElement root, out VisualElement sidePanel, out VisualElement ruleContent,
                out VisualElement ruleButtons, out VisualElement notificationArea) {
    root = new VisualElement();
    root.style.flexDirection = FlexDirection.Column;
    root.style.backgroundColor = BackgroundColor;
    root.style.paddingLeft = 5;
    root.style.paddingRight = 5;
    root.style.paddingTop = 5;
    root.style.paddingBottom = 5;
    root.style.marginBottom = 10;

    var ruleWrapper = new VisualElement();
    ruleWrapper.style.flexDirection = FlexDirection.Row;
    ruleWrapper.style.alignItems = Align.Center;
    root.Add(ruleWrapper);

    sidePanel = new VisualElement();
    sidePanel.style.flexShrink = 1;
    sidePanel.style.alignItems = Align.Center;
    sidePanel.style.marginRight = 5;
    ruleWrapper.Add(this._sidePanel);

    ruleContent = new VisualElement();
    ruleContent.style.flexGrow = 1;
    ruleContent.style.alignItems = Align.Stretch;
    ruleWrapper.Add(ruleContent);

    ruleButtons = new VisualElement();
    ruleButtons.style.flexDirection = FlexDirection.Row;
    ruleButtons.style.alignItems = Align.FlexStart;
    ruleButtons.style.marginTop = 5;
    root.Add(ruleButtons);

    notificationArea = new VisualElement();
    notificationArea.style.flexDirection = FlexDirection.Column;
    notificationArea.style.alignItems = Align.FlexStart;
    notificationArea.style.marginTop = 5;
    notificationArea.ToggleDisplayStyle(false);
    root.Add(notificationArea);
  }

  void Reset() {
    _ruleContent.Clear();
    _sidePanel.Clear();
    _sidePanel.ToggleDisplayStyle(true);
    _ruleButtons.Clear();
    _ruleButtons.ToggleDisplayStyle(true);
    _notificationArea.Clear();
    _notificationArea.ToggleDisplayStyle(false);
  }

  void MarkDeleted() {
    IsDeleted = true;
    OnModeChanged?.Invoke(this, EventArgs.Empty);
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
    }
  }

  #endregion
}
