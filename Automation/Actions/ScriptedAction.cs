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
  public override string UiDescription =>
      _parsedExpression != null
      ? DependencyContainer.GetInstance<ExpressionParser>().GetDescription(_parsedExpression)
      : CommonFormats.HighlightRed(
          Behavior.Loc.T(_parsingResult.ParsedExpression == null ? ParseErrorLocKey : RuntimeErrorLocKey));

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    if (_lastValidatedBehavior == behavior) {
      return _lastValidationResult;
    }
    _lastValidatedBehavior = behavior;
    _lastValidationResult = ParseAndValidate(Expression, behavior) is { ParsedExpression: not null };
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
      if (_parsedExpression.ExecuteOnce) {
        IsMarkedForCleanup = true;
      }
    } catch (ScriptError.Interrupted e) {
      HostedDebugLog.Fine(Behavior, "Action execution interrupted: {0}\nError: {1}", Expression, e.Message);
    } catch (ScriptError e) {
      HostedDebugLog.Error(Behavior, "Action execution failed: {0}\nError: {1}", Expression, e.Message);
      SetParsedExpression(null);
      Behavior.ReportError(this);
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
    if (_parsedExpression != null) {
      SetParsedExpression(null);
    } else {
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

  // Used by the RulesEditor dialog.
  internal static ParsingResult? ParseAndValidate(string expression, AutomationBehavior behavior) {
    var result = DependencyContainer.GetInstance<ExpressionParser>().Parse(expression, behavior);
    if (result.LastError != null) {
      HostedDebugLog.Error(behavior, "Failed to parse action: {0}\nError: {1}", expression, result.LastError);
      return null;
    }
    if (result.ParsedExpression is not ActionOperator) {
      HostedDebugLog.Error(behavior, "Expression is not an action operator: {0}", result.ParsedExpression);
      return null;
    }
    return result;
  }

  void ParseAndApply() {
    var result = ParseAndValidate(Expression, Behavior);
    if (result == null) {
      Behavior.ReportError(this);
      return;
    }
    _parsingResult = result.Value;
    SetParsedExpression(_parsingResult.ParsedExpression);
    Expression = _parsedExpression!.Serialize();
  }

  void SetParsedExpression(IExpression expression) {
    var scriptingService = DependencyContainer.GetInstance<ScriptingService>();
    if (_parsedExpression != null) {
      scriptingService.UninstallActions(_parsedExpression, Behavior);
    }
    _parsedExpression = expression as ActionOperator;
    if (_parsedExpression != null) {
      scriptingService.InstallActions(_parsedExpression, Behavior);
    }
  }

  #endregion
}
