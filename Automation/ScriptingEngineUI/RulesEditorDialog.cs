// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using TimberApi.UIBuilderSystem.StylingElements;
using TimberApi.UIPresets.Buttons;
using TimberApi.UIPresets.ScrollViews;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngineUI;

class RulesEditorDialog : IPostLoadableSingleton {

  const string ConditionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ConditionLabel";
  const string ActionLabelLocKey = "IgorZ.Automation.Scripting.Editor.ActionLabel";
  const string AddRuleFromScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.AddRuleFromScriptBtn";
  const string AddRuleViaConstructorBtnLocKey = "IgorZ.Automation.Scripting.Editor.AddRuleViaConstructorBtn";
  const string DeleteRuleBtnLocKey = "IgorZ.Automation.Scripting.Editor.DeleteRuleBtn";
  const string EditAsScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.EditAsScriptBtn";
  const string EditInConstructorBtnLocKey = "IgorZ.Automation.Scripting.Editor.EditInConstructorBtn";
  const string TestScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.TestScriptBtn";
  const string SaveScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.SaveScriptBtn";
  const string DiscardScriptBtnLocKey = "IgorZ.Automation.Scripting.Editor.DiscardScriptBtn";
  const string ConditionMustBeBoolLocKey = "IgorZ.Automation.Scripting.Editor.ConditionMustBeBoolean";
  const string ActionMustBeActionLocKey = "IgorZ.Automation.Scripting.Editor.ActionMustBeAction";
  const string SaveRulesBtnLocKey = "IgorZ.Automation.Scripting.Editor.SaveRules";
  const string DiscardChangesBtnLocKey = "IgorZ.Automation.Scripting.Editor.DiscardChanges";
  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";

  #region API

  internal sealed class RuleDefinition {
    public string ConditionExpression;
    public BoolOperatorExpr ParsedCondition;
    public string ActionExpression;
    public ActionExpr ParsedAction;
    public VisualElement RuleRow { get; init; }
    public IAutomationAction LegacyRule { get; init; }
  }

  public void Show(AutomationBehavior behavior, Action onClosed) {
    var builder = _dialogBoxShower.Create()
        .SetMaxWidth(_contentMaxWidth)
        .SetConfirmButton(() => SaveRulesAndCloseDialog(onClosed), _uiFactory.T(SaveRulesBtnLocKey))
        .SetCancelButton(() => {}, _uiFactory.T(DiscardChangesBtnLocKey))
        .AddContent(CreateContent());
    builder._root.Q<VisualElement>("Box").style.width = _contentMaxWidth;

    _ruleRowsContainer = UiFactory.FindElementUpstream<VisualElement>(builder._root, "RuleRowsContainer");
    _confirmButton = UiFactory.FindElementUpstream<Button>(builder._root, "ConfirmButton");

    SetActiveBuilding(behavior);
    builder.Show();
  }

  #endregion

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {}

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;
  readonly ITooltipRegistrar _tooltipRegistrar;
  readonly DialogBoxShower _dialogBoxShower;
  readonly ConstructorEditorProvider _constructorEditorProvider;
  readonly int _contentMaxWidth;
  readonly int _contentMaxHeight;

  const int TestScriptStatusHighlightDurationMs = 1000;
  const float ContentWidthRatio = 0.6f;
  const float ContentHeightRatio = 0.6f;

  readonly List<RuleDefinition> _rules = []; 
  readonly List<RuleDefinition> _pendingEditorRules = [];

  VisualElement _ruleRowsContainer;
  AutomationBehavior _activeBuilding;
  Button _confirmButton;

  RulesEditorDialog(UiFactory uiFactory, ITooltipRegistrar tooltipRegistrar, DialogBoxShower dialogBoxShower,
                    ConstructorEditorProvider constructorEditorProvider) {
    _uiFactory = uiFactory;
    _tooltipRegistrar = tooltipRegistrar;
    _dialogBoxShower = dialogBoxShower;
    _constructorEditorProvider = constructorEditorProvider;
    _contentMaxWidth = Mathf.RoundToInt(Screen.width * ContentWidthRatio);
    _contentMaxHeight = Mathf.RoundToInt(Screen.height * ContentHeightRatio);
  }

  VisualElement CreateContent() {
    var root = new VisualElement {
        style = {
            minHeight = _contentMaxHeight,
        },
    };
    root.Add(new VisualElement() {
        name = "RuleRowsContainer",//FIXME: constant
    });
    var buttons = new VisualElement {
        style = {
            flexDirection = FlexDirection.Row,
            marginTop = 10,
        },
    };
    root.Add(buttons);

    var addRuleFromScriptBtn = _uiFactory.CreateButton(AddRuleFromScriptBtnLocKey, AddScript);
    addRuleFromScriptBtn.style.marginRight = 5;
    buttons.Add(addRuleFromScriptBtn);
    var addRuleViaConstructorBtn =
        _uiFactory.CreateButton(AddRuleViaConstructorBtnLocKey, _ => EditInConstructor(null));
    addRuleViaConstructorBtn.style.marginRight = 5;
    buttons.Add(addRuleViaConstructorBtn);

    var scrollView = _uiFactory.UiBuilder.Create<DefaultScrollView>()
        .SetMaxHeight(_contentMaxHeight)
        .BuildAndInitialize();
    scrollView.Add(root);

    return scrollView;
  }

  void SetActiveBuilding(AutomationBehavior behavior) {
    _activeBuilding = behavior;
    _rules.Clear();
    _pendingEditorRules.Clear();
    _ruleRowsContainer.Clear();

    foreach (var action in behavior.Actions) {
      RuleDefinition rule;
      if (action is ScriptedAction { Condition: ScriptedCondition scriptedCondition } scriptedAction) {
        rule = AddRule(scriptedCondition.Expression, scriptedAction.Expression);
      } else {
        rule = AddRule(action.Condition.UiDescription, action.UiDescription, action);
      }
      ViewRulePlain(rule);
    }
  }

  RuleDefinition AddRule(string condition, string action, IAutomationAction legacyRule = null) {
    var rule = new RuleDefinition {
        RuleRow = MakeRuleRow(),
        ConditionExpression = condition ?? "",
        ActionExpression = action ?? "",
        LegacyRule = legacyRule,
    };
    ParseRule(rule);
    _rules.Add(rule);
    _ruleRowsContainer.Add(rule.RuleRow);
    return rule;
  }

  void ParseRule(RuleDefinition rule) {
    rule.ParsedCondition = ParseExpression(rule.ConditionExpression).ParsedExpression as BoolOperatorExpr;
    rule.ParsedAction = ParseExpression(rule.ActionExpression).ParsedExpression as ActionExpr;
  }

  void RemoveRule(RuleDefinition rule) {
    _rules.Remove(rule);
    rule.RuleRow.RemoveFromHierarchy();
    RemovePendingEdit(rule);
  }

  void SaveRulesAndCloseDialog(Action onClosed) {
    _activeBuilding.ClearAllRules();
    foreach (var rule in _rules) {
      if (rule.LegacyRule != null) {
        _activeBuilding.AddRule(rule.LegacyRule.Condition, rule.LegacyRule);
      } else {
        var condition = new ScriptedCondition();
        condition.SetExpression(rule.ConditionExpression);
        var action = new ScriptedAction();
        action.SetExpression(rule.ActionExpression);
        _activeBuilding.AddRule(condition, action);
      }
    }
    onClosed?.Invoke();
  }

  void AddScript() {
    var rule = AddRule(null, null);
    EditRuleAsScript(rule, isNew: true);
  }

  void EditInConstructor(RuleDefinition rule) {
    var isNew = rule == null;
    rule ??= AddRule(null, null);
    AddPendingEdit(rule);

    _constructorEditorProvider.MakeForRule(rule, _activeBuilding, out var applyFn);
    var buttons = rule.RuleRow.Q("RuleButtons");
    buttons.Clear();
    CreateButton(buttons, SaveScriptBtnLocKey, _ => {
      var error = applyFn?.Invoke();
      if (error != null) {
        ReportError(rule, error);
        return;
      }
      RemovePendingEdit(rule);
      ParseRule(rule);
      ClearError(rule);
      ViewRulePlain(rule);
    });
    CreateButton(buttons, DiscardScriptBtnLocKey, _ => {
      if (isNew) {
        RemoveRule(rule);
      } else {
        RemovePendingEdit(rule);
        ClearError(rule);
        ViewRulePlain(rule);
      }
    });
  }

  void ViewRulePlain(RuleDefinition rule) {
    // Side panel
    var sidePanel = rule.RuleRow.Q("SidePanel");
    sidePanel.Clear();
    sidePanel.ToggleDisplayStyle(true);
    var deleteBtn = _uiFactory.UiBuilder.Create<CrossButton>().BuildAndInitialize();
    _tooltipRegistrar.Register(deleteBtn, _uiFactory.T(DeleteRuleBtnLocKey));
    deleteBtn.clicked += () => RemoveRule(rule);
    sidePanel.Add(deleteBtn);

    // Rule content.
    var content = rule.RuleRow.Q("RuleContent");
    content.Clear();
    var label = _uiFactory.CreateLabel();
    GetDescriptions(rule, out var conditionDesc, out var actionDesc);
    label.text = _activeBuilding.Loc.T(ConditionLabelLocKey) + " " + conditionDesc
        + "\n" + _activeBuilding.Loc.T(ActionLabelLocKey) + " " + actionDesc;
    content.Add(label);

    // Controls.
    var buttons = rule.RuleRow.Q("RuleButtons");
    buttons.Clear();

    if (rule.LegacyRule is not null) {
      buttons.ToggleDisplayStyle(false);
    } else {
      buttons.ToggleDisplayStyle(true);
      CreateButton(buttons, EditAsScriptBtnLocKey, _ => {
        EditRuleAsScript(rule);
      });
      var btn = CreateButton(buttons, EditInConstructorBtnLocKey, _ => EditInConstructor(rule));
      btn.SetEnabled(VerifyIfEditableInConstructor(rule));
    }
  }

  bool VerifyIfEditableInConstructor(RuleDefinition rule) {
    var conditionParserContext = ParseExpression(rule.ConditionExpression);
    if (conditionParserContext.LastError != null) {
      return false;
    }
    var actionParserContext = ParseExpression(rule.ActionExpression);
    if (actionParserContext.LastError != null) {
      return false;
    }
    
    if (conditionParserContext.ParsedExpression is not BinaryOperatorExpr) {
      return false;
    }
    if (conditionParserContext.ParsedExpression is BinaryOperatorExpr { Right: not ConstantValueExpr }) {
      return false;
    }
    if (actionParserContext.ParsedExpression is not ActionExpr actionExpr
        || actionExpr.Operands.Count > 2 || actionExpr.Operands.Skip(1).Any(o => o is not ConstantValueExpr)) {
      return false;
    }
    return true;
  }

  void GetDescriptions(RuleDefinition rule, out string condition, out string action) {
    if (rule.LegacyRule is not null) {
      condition = rule.ConditionExpression;
      action = rule.ActionExpression;
      return;
    }

    //FIXME: take parsed expressions from the rule
    var conditionParserContext = ParseExpression(rule.ConditionExpression);
    if (conditionParserContext.LastError != null) {
      condition = _uiFactory.T(ParseErrorLocKey);
      DebugEx.Error("Failed to parse condition: {0}\nError: {1}", rule.ConditionExpression, conditionParserContext.LastError);
    } else {
      condition = ExpressionParser.Instance.GetDescription(conditionParserContext);
      condition = TextColors.ColorizeText($"<SolidHighlight>{condition}</SolidHighlight>");
    }

    var actionParserContext = ParseExpression(rule.ActionExpression);

    if (actionParserContext.LastError != null) {
      action = _uiFactory.T(ParseErrorLocKey);
      DebugEx.Error("Failed to parse action: {0}\nError: {1}", rule.ActionExpression, actionParserContext.LastError);
    } else {
      action = ExpressionParser.Instance.GetDescription(actionParserContext);
      action = TextColors.ColorizeText($"<SolidHighlight>{action}</SolidHighlight>");
    }
  }

  void AddPendingEdit(RuleDefinition rule) {
    _pendingEditorRules.Add(rule);
    _confirmButton.SetEnabled(false);
  }

  void RemovePendingEdit(RuleDefinition rule) {
    _pendingEditorRules.Remove(rule);
    _confirmButton.SetEnabled(_pendingEditorRules.Count == 0);
  }

  void EditRuleAsScript(RuleDefinition rule, bool isNew = false) {
    AddPendingEdit(rule);

    // Side panel
    rule.RuleRow.Q("SidePanel").ToggleDisplayStyle(false);

    // Rule content.
    var content = rule.RuleRow.Q("RuleContent");
    content.Clear();

    var conditionEdit = _uiFactory.CreateTextField();
    conditionEdit.style.flexGrow = 1;
    conditionEdit.textInput.style.unityTextAlign = TextAnchor.MiddleLeft;
    conditionEdit.value = rule.ConditionExpression;
    content.Add(CreateRow(
        _uiFactory.CreateLabel(ConditionLabelLocKey),
        conditionEdit
    ));

    var actionEdit = _uiFactory.CreateTextField();
    actionEdit.style.flexGrow = 1;
    actionEdit.textInput.style.unityTextAlign = TextAnchor.MiddleLeft;
    actionEdit.value = rule.ActionExpression;
    content.Add(CreateRow(
        _uiFactory.CreateLabel(ActionLabelLocKey),
        actionEdit
    ));

    // Buttons
    var buttons = rule.RuleRow.Q("RuleButtons");
    buttons.Clear();

    CreateButton(buttons, TestScriptBtnLocKey, btn => {
      VisualEffects.ScheduleSwitchEffect(
          btn, TestScriptStatusHighlightDurationMs, false, true,
          (b, v) => b.SetEnabled(v));
      if (!RunRuleCheck(rule.RuleRow, conditionEdit, actionEdit)) {
        return;
      }
      VisualEffects.ScheduleSwitchEffect(
          conditionEdit, TestScriptStatusHighlightDurationMs, Color.green, UiFactory.DefaultColor,
          (c, v) => c.textInput.style.color = v);
      VisualEffects.ScheduleSwitchEffect(
          actionEdit, TestScriptStatusHighlightDurationMs, Color.green, UiFactory.DefaultColor,
          (c, v) => c.textInput.style.color = v);
    });
    CreateButton(buttons, SaveScriptBtnLocKey, _ => {
      if (RunRuleCheck(rule.RuleRow, conditionEdit, actionEdit)) {
        RemovePendingEdit(rule);
        rule.ConditionExpression = conditionEdit.value;
        rule.ActionExpression = actionEdit.value;
        ParseRule(rule);
        ClearError(rule);
        ViewRulePlain(rule);
      }
    });
    CreateButton(buttons, DiscardScriptBtnLocKey, _ => {
      if (isNew) {
        RemoveRule(rule);
      } else {
        RemovePendingEdit(rule);
        ClearError(rule);
        ViewRulePlain(rule);
      }
    });
  }

  bool RunRuleCheck(VisualElement ruleRow, TextField conditionEdit, TextField actionEdit) {
    conditionEdit.textInput.style.color = UiFactory.DefaultColor;
    actionEdit.textInput.style.color = UiFactory.DefaultColor;
    var notifications = ruleRow.Q("NotificationArea");
    notifications.Clear();//FIXME: clear on apply.
    if (CheckExpressionAndShowError(ruleRow, conditionEdit, true)
        && CheckExpressionAndShowError(ruleRow, actionEdit, false)) {
      notifications.ToggleDisplayStyle(false);
      return true;
    }
    notifications.ToggleDisplayStyle(true);
    return false;
  }

  bool CheckExpressionAndShowError(VisualElement ruleRow, TextField expressionField, bool isCondition) {
    var parserContext = ParseExpression(expressionField.value);
    var error = parserContext.LastError;
    if (error == null) {
      if (isCondition) {
        if (parserContext.ParsedExpression is not BoolOperatorExpr) {
          error = _activeBuilding.Loc.T(ConditionMustBeBoolLocKey);
        }
      } else {
        if (parserContext.ParsedExpression is not ActionExpr) {
          error = _activeBuilding.Loc.T(ActionMustBeActionLocKey);
        }
      }
    }
    if (error == null) {
      return true;
    }
    ReportError(ruleRow, error);
    VisualEffects.ScheduleSwitchEffect(
        expressionField,
        TestScriptStatusHighlightDurationMs, new Color(1, 0, 0, 0.2f), Color.clear,
        (f, c) => f.textInput.style.backgroundColor = c);
    return false;
  }

  ParserContext ParseExpression(string expression) {
    var parserContext = new ParserContext() {
      ScriptHost = _activeBuilding,
    };
    //FIXME: inject
    ExpressionParser.Instance.Parse(expression, parserContext);
    return parserContext;
  }

  Button CreateButton(VisualElement parent, string locKey, Action<Button> onClick) {
    var button = _uiFactory.CreateButton(locKey, onClick, new Padding(0, 5, 0, 5));
    button.style.marginRight = 5;
    parent.Add(button);
    return button;
  }

  static VisualElement MakeRuleRow() {
    var wrapper = new VisualElement {
      style = {
        flexDirection = FlexDirection.Column,
        backgroundColor = new Color(1, 1, 1, 0.05f),
        paddingLeft = 5,
        paddingRight = 5,
        paddingTop = 5,
        paddingBottom = 5,
        marginBottom = 10,
      },
    };

    var ruleWrapper = new VisualElement {
      style = {
        flexDirection = FlexDirection.Row,
        alignItems = Align.Center,
      },
    };
    wrapper.Add(ruleWrapper);

    ruleWrapper.Add(new VisualElement {
      name = "SidePanel",
      style = {
        flexShrink = 1,
        alignItems = Align.Center,
        marginRight = 5,
      },
    });

    ruleWrapper.Add(new VisualElement {
      name = "RuleContent",
      style = {
        flexGrow = 1,
        alignItems = Align.Stretch,
      },
    });

    wrapper.Add(new VisualElement {
      name = "RuleButtons",
      style = {
        flexDirection = FlexDirection.Row,
        alignItems = Align.FlexStart,
        marginTop = 5,
      },
    });

    wrapper.Add(new VisualElement {
      name = "NotificationArea",
      style = {
        flexDirection = FlexDirection.Column,
        alignItems = Align.FlexStart,
        marginTop = 5,
      },
    });
    wrapper.Q("NotificationArea").ToggleDisplayStyle(false);

    return wrapper;
  }

  static VisualElement CreateRow(params VisualElement[] elements) {
    var row = new VisualElement {
        style = {
            flexDirection = FlexDirection.Row,
            alignItems = Align.Center,
        },
    };
    for (var i = 0; i < elements.Length; i++) {
      var element = elements[i];
      if (i != elements.Length - 1) {
        element.style.marginRight = 5;
      }
      row.Add(element);
    }
    return row;
  }

  static void ReportError(RuleDefinition rule, string error) {
    ReportError(rule.RuleRow, error);
  }

  static void ReportError(VisualElement ruleRow, string error) {
    var notifications = ruleRow.Q("NotificationArea");
    notifications.Clear();
    var errorLabel = new Label {
        style = {
            color = Color.red,
        },
        text = error,
    };
    notifications.Add(errorLabel);
    notifications.ToggleDisplayStyle(true);
  }

  static void ClearError(RuleDefinition rule) {
    rule.RuleRow.Q("NotificationArea").ToggleDisplayStyle(false);
  }

  #endregion
}
