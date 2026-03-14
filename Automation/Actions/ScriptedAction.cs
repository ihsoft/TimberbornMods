// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.ScriptingEngineUI;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.Actions;

sealed class ScriptedAction : AutomationActionBase {

  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";

  #region AutomationActionBase overrides

  /// <inheritdoc/>
  public override bool IsInErrorState => _lastScriptError != null;

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    var clone = (ScriptedAction)base.CloneDefinition();
    clone.Expression = Expression;
    return clone;
  }

  /// <inheritdoc/>
  public override string UiDescription {
    get {
      //FIXME: return null by design and move logic to UI helpers.
      if (_lastScriptError != null) {
        return CommonFormats.HighlightRed(Behavior.Loc.T(_lastScriptError));
      }
      var expressionDescriber = StaticBindings.DependencyContainer.GetInstance<ExpressionDescriber>();
      try {
        return CommonFormats.HighlightYellow(expressionDescriber.DescribeExpression(_parsedExpression));
      } catch (ScriptError.RuntimeError e) {
        return CommonFormats.HighlightRed(Behavior.Loc.T(e.LocKey));
      }
    }
  }
  string _lastScriptError;

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    if (_lastValidatedBehavior == behavior) {
      return _lastValidationResult;
    }
    _lastValidatedBehavior = behavior;
    _lastValidationResult = ParseAndValidate(Expression, behavior, out var parsingResult) != null;
    if (parsingResult.LastScriptError is ScriptError.BadStateError error) {
      DebugEx.Fine("Expression '{0}' is not valid at {1}: {2}", Expression, behavior, error.Message);
    }
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
    ParseAndApply();
    if (!Condition.IsEnabled) {
      return;
    }
    if (_lastScriptError != null) {
      Behavior.ReportError(this);  // The error can be a runtime error, loaded from the persistent state.
      return;
    }
    _installedActions = ScriptingService.Instance.InstallActions(_parsedExpression, Behavior);
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    if (_installedActions != null) {
      ScriptingService.Instance.UninstallActions(_installedActions, Behavior);
      _installedActions = null;
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

  /// <summary>Result of parsing the expression.</summary>
  public ParsingResult ParsingResult => _parsingResult;

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

  static ActionOperator ParseAndValidate(
      string expression, AutomationBehavior behavior, out ParsingResult parsingResult) {
    var parserFactory = StaticBindings.DependencyContainer.GetInstance<ParserFactory>();
    var actionOperator = parserFactory.ParseAction(
        expression, behavior, out parsingResult, preferredParser: parserFactory.LispSyntaxParser);
    if (parsingResult.LastError != null) {
      HostedDebugLog.Error(behavior, "Failed to parse action: {0}\nError: {1}", expression, parsingResult.LastError);
    }
    return actionOperator;
  }

  void ParseAndApply() {
    if (_parsingResult != default) {
      throw new InvalidOperationException($"{nameof(ParseAndApply)} should only be called once.");
    }
    ResetScriptError();
    _parsedExpression = ParseAndValidate(Expression, Behavior, out _parsingResult);
    if (_parsedExpression == null) {
      _lastScriptError = ParseErrorLocKey;
      return;
    }
    Behavior.IncrementStateVersion();
    Expression = StaticBindings.DependencyContainer.GetInstance<LispSyntaxParser>().Decompile(_parsedExpression);
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
