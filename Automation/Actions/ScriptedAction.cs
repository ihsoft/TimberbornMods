// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.Localization;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.Actions;

sealed class ScriptedAction : AutomationActionBase {

  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";

  #region AutomationActionBase overrides

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new ScriptedAction { TemplateFamily = TemplateFamily };
  }

  /// <inheritdoc/>
  public override string UiDescription => _uiDescription;
  string _uiDescription;

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    //FIXME: parse te scriptable anme and check rtestricted type if any.
    return true;
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState) {
      return;
    }
    if (_parsedExpression != null) {
      HostedDebugLog.Fine(Behavior, "Condition triggered: {0}", automationCondition);
      _parsedExpression.Execute();
    } else {
      HostedDebugLog.Error(Behavior, "Condition triggered, but the action was broken: {0}", Expression);
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    base.OnBehaviorAssigned();
    ParseAction();
  }

  #endregion

  #region API

  /// <summary>Script code for expression to execute.</summary>
  /// <remarks>
  /// It must be <see cref="ActionExpr"/> expression. Example of an action: "(act Floodgate.SetHeight 150)". 
  /// </remarks>
  /// <seealso cref="ActionExpr"/>
  // ReSharper disable once MemberCanBePrivate.Global
  public string Expression { get; private set; }

  /// <summary>Sets the action expression.</summary>
  /// <remarks>Can only be set on the non-active condition.</remarks>
  /// <seealso cref="Expression"/>
  public void SetExpression(string expression) {
    if (Behavior) {
      throw new InvalidOperationException("Cannot change action when the behavior is assigned.");
    }
    Expression = expression;
  }

  #endregion

  #region IGameSerializable implemenation

  static readonly PropertyKey<string> ExpressionKey = new("Expression");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    Expression = objectLoader.Get(ExpressionKey);
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ExpressionKey, Expression);
  }

  #endregion

  #region Implementation

  ParserContext _parserParserContext;
  ActionExpr _parsedExpression;

  void ParseAction() {
    _parserParserContext = new ParserContext {
        ScriptHost = Behavior,
    };
    var res = Behavior.AutomationService.ExpressionParser.Parse(Expression, _parserParserContext);
    if (!res) {
      HostedDebugLog.Error(
          Behavior, "Failed to parse action: {0}\nError: {1}", Expression, _parserParserContext.LastError);
      _uiDescription = TextColors.ColorizeText(Behavior.Loc.T(ParseErrorLocKey));
      return;
    }
    _parsedExpression = _parserParserContext.ParsedExpression as ActionExpr;
    if (_parsedExpression == null) {
      HostedDebugLog.Error(
          Behavior, "Expression is not an action operator: {0}", _parserParserContext.ParsedExpression);
      _uiDescription = TextColors.ColorizeText(Behavior.Loc.T(ParseErrorLocKey));
      return;
    }
    var description = ExpressionParser.Instance.GetDescription(_parserParserContext);
    _uiDescription = TextColors.ColorizeText($"<SolidHighlight>{description}</SolidHighlight>");
  }

  #endregion
}
