// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using TimberApi.DependencyContainerSystem;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.Actions;

sealed class ScriptedAction : AutomationActionBase {

  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";

  #region AutomationActionBase overrides

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new ScriptedAction { TemplateFamily = TemplateFamily, Expression = Expression };
  }

  /// <inheritdoc/>
  public override string UiDescription {
    get {
      if (_lastScriptError != null) {
        return CommonFormats.HighlightRed(Behavior.Loc.T(_lastScriptError));
      }
      return CommonFormats.HighlightYellow(
          DependencyContainer.GetInstance<ExpressionParser>().GetDescription(_parsedExpression));
    }
  }
  string _lastScriptError;

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
      ResetScriptError();
      _parsedExpression.Execute();
      if (_parsedExpression is { ExecuteOnce: true }) {
        MarkForCleanup();
      }
    } catch (ScriptError.RuntimeError e) {
      if (_lastScriptError != null) {
        throw;  // Can be already handled upstream in case of recursive calls.
      }
      ReportScriptError(e);
      throw;
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    base.OnBehaviorAssigned();
    if (_lastScriptError != null) {
      Behavior.ReportError(this);
    }
    ParseAndApply();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    base.OnBehaviorToBeCleared();
    if (_installedActions != null) {
      var scriptingService = DependencyContainer.GetInstance<ScriptingService>();
      scriptingService.UninstallActions(_installedActions, Behavior);
    }
    ResetScriptError();
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
  static readonly PropertyKey<string> HasScriptErrorKey = new("ScriptError");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    Expression = objectLoader.Get(ExpressionKey);
    _lastScriptError = objectLoader.GetValueOrDefault(HasScriptErrorKey, null);
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ExpressionKey, Expression);
    if (_lastScriptError != null) {
      objectSaver.Set(HasScriptErrorKey, _lastScriptError);
    }
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
      ResetScriptError();
      _lastScriptError = CommonFormats.HighlightRed(Behavior.Loc.T(ParseErrorLocKey));
      Behavior.ReportError(this);
      return;
    }
    Behavior.IncrementStateVersion();
    Expression = _parsedExpression.Serialize();
    _installedActions = DependencyContainer.GetInstance<ScriptingService>().InstallActions(_parsedExpression, Behavior);
  }

  void ReportScriptError(ScriptError.RuntimeError e) {
    _lastScriptError = e.LocKey;
    Behavior.ReportError(this);
  }

  void ResetScriptError() {
    if (_lastScriptError == null) {
      return;  // No runtime error to reset.
    }
    _lastScriptError = null;
    Behavior.ClearError(this);
  }

  #endregion
}
