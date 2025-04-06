// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using TimberApi.DependencyContainerSystem;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.InputSystem;

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
  public override string UiDescription => _uiDescription
      ?? CommonFormats.HighlightYellow(
          DependencyContainer.GetInstance<ExpressionParser>().GetDescription(_parsedExpression));
  string _uiDescription;

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    if (_lastValidatedBehavior == behavior) {
      return _lastValidationResult;
    }
    _lastValidatedBehavior = behavior;
    var result = ExpressionParser.Instance.Parse(Expression, behavior);
    _lastValidationResult = result.ParsedExpression != null;
    if (!_lastValidationResult && Keyboard.current.ctrlKey.isPressed) {
      HostedDebugLog.Warning(behavior, "Validation didn't pass: {0}\n{1}", Expression, result.LastScriptError);
    }
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
      _uiDescription = null;
      _parsedExpression.Execute();
    } catch (ExecutionInterrupted e) {
      HostedDebugLog.Fine(Behavior, "Action execution interrupted: {0}\nReason: {1}", Expression, e.Reason);
      _uiDescription = CommonFormats.HighlightRed(Behavior.Loc.T(RuntimeErrorLocKey));
    } catch (ScriptError e) {
      HostedDebugLog.Error(Behavior, "Action failed: {0}\nError: {1}", Expression, e.Message);
      _uiDescription = CommonFormats.HighlightRed(Behavior.Loc.T(RuntimeErrorLocKey));
      _parsedExpression = null;
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

  ParsingResult _parsingResult;
  ActionExpr _parsedExpression;

  void ParseAndApply() {
    _uiDescription = null;
    _parsingResult = DependencyContainer.GetInstance<ExpressionParser>().Parse(Expression, Behavior);
    if (_parsingResult.LastError != null) {
      HostedDebugLog.Error(
          Behavior, "Failed to parse action: {0}\nError: {1}", Expression, _parsingResult.LastError);
      _uiDescription = CommonFormats.HighlightRed(Behavior.Loc.T(ParseErrorLocKey));
      return;
    }
    _parsedExpression = _parsingResult.ParsedExpression as ActionExpr;
    if (_parsedExpression == null) {
      HostedDebugLog.Error(
          Behavior, "Expression is not an action operator: {0}", _parsingResult.ParsedExpression);
      _uiDescription = CommonFormats.HighlightRed(Behavior.Loc.T(ParseErrorLocKey));
      return;
    }

    Expression = _parsedExpression.Serialize();
  }

  #endregion
}
