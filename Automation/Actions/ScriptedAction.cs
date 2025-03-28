// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.Actions;

sealed class ScriptedAction : AutomationActionBase {

  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";
  const string RuntimeErrorLocKey = "IgorZ.Automation.Scripting.Expressions.RuntimeError";

  #region AutomationActionBase overrides

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new ScriptedAction { TemplateFamily = TemplateFamily, Expression = Expression };
  }

  /// <inheritdoc/>
  public override string UiDescription => _uiDescription;
  string _uiDescription;

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    if (_lastValidatedBehavior == behavior) {
      return _lastValidationResult;
    }
    _lastValidatedBehavior = behavior;
    _parserParserContext = new ParserContext {
        ScriptHost = behavior,
    };
    ExpressionParser.Instance.Parse(Expression, _parserParserContext);
    _lastValidationResult = _parserParserContext.ParsedExpression != null;
    return _lastValidationResult;
  }

  AutomationBehavior _lastValidatedBehavior;
  bool _lastValidationResult;

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState) {
      return;
    }
    if (_parsedExpression == null) {
      HostedDebugLog.Error(Behavior, "Condition triggered, but the action was broken: {0}", Expression);
      return;
    }
    try {
      _parsedExpression.Execute();
    } catch (ExecutionInterrupted e) {
      HostedDebugLog.Fine(Behavior, "Action execution interrupted: {0}\nReason: {1}", Expression, e.Reason);
    } catch (ScriptError e) {
      HostedDebugLog.Error(Behavior, "Action failed: {0}\nError: {1}", Expression, e.Message);
      _parsedExpression = null;
      _uiDescription = Behavior.Loc.T(RuntimeErrorLocKey);
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    base.OnBehaviorAssigned();
    ParseAndApply();
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

  void ParseAndApply() {
    _parserParserContext = new ParserContext {
        ScriptHost = Behavior,
    };
    ExpressionParser.Instance.Parse(Expression, _parserParserContext);
    if (_parserParserContext.LastError != null) {
      HostedDebugLog.Error(
          Behavior, "Failed to parse action: {0}\nError: {1}", Expression, _parserParserContext.LastError);
      _uiDescription = Behavior.Loc.T(ParseErrorLocKey);
      return;
    }
    _parsedExpression = _parserParserContext.ParsedExpression as ActionExpr;
    if (_parsedExpression == null) {
      HostedDebugLog.Error(
          Behavior, "Expression is not an action operator: {0}", _parserParserContext.ParsedExpression);
      _uiDescription = Behavior.Loc.T(ParseErrorLocKey);
      return;
    }

    var context = _parserParserContext with { };
    var description = ExpressionParser.Instance.GetDescription(context);
    _uiDescription = context.LastError == null ? CommonFormats.HighlightYellow(description) : description;

    Expression = _parsedExpression.Serialize();
  }

  #endregion
}
