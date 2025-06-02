// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using TimberApi.DependencyContainerSystem;
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
  public override string UiDescription {
    get {
      if (_staticDescription != null) {
        return _staticDescription;
      }
      return CommonFormats.HighlightYellow(
          DependencyContainer.GetInstance<ExpressionParser>().GetDescription(_parsedExpression));
    }
  }
  string _staticDescription;

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    if (_lastValidatedBehavior == behavior) {
      return _lastValidationResult;
    }
    _lastValidatedBehavior = behavior;
    _lastValidationResult = ParseAndValidate(Expression, behavior, out _parsingResult) != null;
    return _lastValidationResult;
  }

  AutomationBehavior _lastValidatedBehavior;
  bool _lastValidationResult;

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState || IsMarkedForCleanup) {
      return;
    }
    if (_parsedExpression == null) {
      HostedDebugLog.Error(Behavior, "Condition triggered, but the action was broken: {0}", Expression);
      return;
    }
    try {
      _parsedExpression.Execute();
      if (_parsedExpression is { ExecuteOnce: true }) {
        IsMarkedForCleanup = true;
      }
    } catch (ScriptError) {
      if (_parsedExpression == null) {
        throw;  // Can be already handled upstream in case of recursive calls.
      }
      _parsedExpression = null;
      _staticDescription = CommonFormats.HighlightRed(Behavior.Loc.T(RuntimeErrorLocKey));
      Behavior.ReportError(this);
      throw;
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    base.OnBehaviorAssigned();
    ParseAndApply();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    base.OnBehaviorToBeCleared();
    if (_installedActions != null) {
      var scriptingService = DependencyContainer.GetInstance<ScriptingService>();
      scriptingService.UninstallActions(_installedActions, Behavior);
    }
    if (_parsedExpression == null) {
      Behavior.ClearError(this);
    }
  }

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType()}(Expr=\"{Expression}\";Condition={Condition})";
  }

  #endregion

  #region API

  /// <summary>Script code for expression to execute.</summary>
  /// <remarks>
  /// It must be <see cref="ActionOperator"/> expression. Example of an action: "(act Floodgate.SetHeight 150)". 
  /// </remarks>
  /// <seealso cref="ActionOperator"/>
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
  ActionOperator _parsedExpression;
  List<ActionOperator> _installedActions;

  // Used by the RulesEditor dialog.
  internal static ActionOperator ParseAndValidate(
      string expression, AutomationBehavior behavior, out ParsingResult parsingResult) {
    parsingResult = DependencyContainer.GetInstance<ExpressionParser>().Parse(expression, behavior);
    if (parsingResult.LastError != null) {
      HostedDebugLog.Error(behavior, "Failed to parse action: {0}\nError: {1}", expression, parsingResult.LastError);
      return null;
    }
    if (parsingResult.ParsedExpression is not ActionOperator actionOperator) {
      HostedDebugLog.Error(behavior, "Expression is not an action operator: {0}", parsingResult.ParsedExpression);
      return null;
    }
    return actionOperator;
  }

  void ParseAndApply() {
    if (_parsingResult != default) {
      throw new InvalidOperationException("ParseAndApply should only be called once.");
    }
    _parsedExpression = ParseAndValidate(Expression, Behavior, out _parsingResult);
    if (_parsedExpression == null) {
      _staticDescription = CommonFormats.HighlightRed(Behavior.Loc.T(ParseErrorLocKey));
      Behavior.ReportError(this);
      return;
    }
    _installedActions = DependencyContainer.GetInstance<ScriptingService>().InstallActions(_parsedExpression, Behavior);
    Expression = _parsedExpression.Serialize();
    Behavior.IncrementStateVersion();
  }

  #endregion
}
