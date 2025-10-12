// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using TimberApi.DependencyContainerSystem;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.InputSystem;

namespace IgorZ.Automation.Conditions;

sealed class ScriptedCondition : AutomationConditionBase, ISignalListener {

  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";

  #region AutomationConditionBase overrides

  /// <inheritdoc/>
  public override bool IsInErrorState => _lastScriptError != null;

  /// <inheritdoc/>
  public override bool CanRunOnUnfinishedBuildings => _canRunOnUnfinishedBuildings;
  bool _canRunOnUnfinishedBuildings;

  /// <inheritdoc/>
  public override string UiDescription {
    get {
      if (_lastScriptError != null) {
        return CommonFormats.HighlightRed(Behavior.Loc.T(_lastScriptError));
      }
      try {
        var describe = _parsedExpression.Describe();
        return ConditionState ? CommonFormats.HighlightGreen(describe) : CommonFormats.HighlightYellow(describe); 
      } catch (ScriptError.RuntimeError e) {
        return CommonFormats.HighlightRed(Behavior.Loc.T(e.LocKey));
      }
    }
  }
  string _lastScriptError;

  /// <inheritdoc/>
  public override void Activate(bool noTrigger = false) {
    base.Activate(noTrigger);
    if (_parsedExpression == null) {
      HostedDebugLog.Warning(Behavior, "Condition parse failed, cannot sync state: {0}", Expression);
      return;  // The condition can't be parsed, no need to sync.
    }

    // Only activate and verify, no side effects expected.
    if (noTrigger) {
      try {
        var newState = _parsedExpression.Execute();
        if (ConditionState != newState) {
          HostedDebugLog.Warning(
              Behavior, "Condition state mismatch: loaded={0}, calculated={1}", ConditionState, newState);
        }
      } catch (ScriptError.RuntimeError e) {
        // The behavior error state is expected to be loaded.
        HostedDebugLog.Error(Behavior, "Activation failed: {0}", e.Message);
      }
      return;
    }

    // Check the condition and trigger side effects if needed.
    try {
      CheckOperands();
    } catch (ScriptError.RuntimeError e) {
      // The behavior error state is already set.
      HostedDebugLog.Error(Behavior, "Activation failed: {0}", e.Message);
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    if (_lastScriptError != null) {
      Behavior.ReportError(this);
    }
    ParseAndApply();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    if (_registeredSignals != null) {
      var scriptingService = DependencyContainer.GetInstance<ScriptingService>();
      scriptingService.UnregisterSignals(_registeredSignals, this);
    }
    ResetScriptError();
  }

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new ScriptedCondition { Expression = Expression, Precondition = Precondition };
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    if (_lastValidatedBehavior == behavior) {
      return _lastValidationResult;
    }
    _lastValidatedBehavior = behavior;
    if (CheckPrecondition(behavior)) {
      var expression = ParseAndValidate(Expression, behavior, out var parsingResult, onlyCheck: true);
      _lastValidationResult = expression != null;
      if (parsingResult.LastScriptError is ScriptError.BadStateError error) {
        DebugEx.Fine("Expression '{0}' is not valid at {1}: {2}", Expression, behavior, error.Message);
      }
    } else {
      _lastValidationResult = false;
    }
    return _lastValidationResult;
  }

  AutomationBehavior _lastValidatedBehavior;
  bool _lastValidationResult;

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType()}(Pre=\"{Precondition}\";Expr=\"{Expression}\")";
  }

  #endregion

  #region API

  /// <summary>Script code for expression to check.</summary>
  /// <remarks>
  /// It must be a boolean operator. See <see cref="BoolOperator"/> for the list of conditions. Example of a
  /// condition: "(and (eq (sig Weather.Season) 'DroughtWeather') (gt Floodgate.Height 0.5))".
  /// </remarks>
  // ReSharper disable once MemberCanBePrivate.Global
  public string Expression { get; private set; } = "";

  /// <summary>Script code for precondition to check.</summary>
  /// <remarks>
  /// It must be a boolean operator. See <see cref="BoolOperator"/> for the list of conditions. If the condition
  /// evaluates to "false", then <see cref="Expression"/> cannot be applied to the selected entity. Note that any
  /// parsing errors in the precondition will be silently ignored.
  /// </remarks>
  // ReSharper disable once MemberCanBePrivate.Global
  public string Precondition { get; set; }

  /// <summary>Sets the condition expression.</summary>
  /// <remarks>Can only be set on the non-active condition.</remarks>
  /// <seealso cref="Expression"/>
  public void SetExpression(string expression) {
    if (Behavior) {
      throw new InvalidOperationException("Cannot change conditions when the behavior is assigned.");
    }
    Expression = expression;
  }

  /// <summary>Result of parsing the expression.</summary>
  public ParsingResult ParsingResult => _parsingResult;

  #endregion

  #region IGameSerializable implemenation

  static readonly PropertyKey<string> ExpressionKey = new("Expression");
  static readonly PropertyKey<string> HasScriptErrorKey = new("ScriptError");
  static readonly PropertyKey<string> PreconditionKey = new("Precondition");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    Expression = objectLoader.Get(ExpressionKey);
    _lastScriptError = objectLoader.GetValueOrDefault(HasScriptErrorKey, null);
    Precondition = objectLoader.GetValueOrDefault(PreconditionKey, null);
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ExpressionKey, Expression);
    if (_lastScriptError != null) {
      objectSaver.Set(HasScriptErrorKey, _lastScriptError);
    }
    if (!string.IsNullOrEmpty(Precondition)) {
      objectSaver.Set(PreconditionKey, Precondition);
    }
  }

  #endregion

  #region ISignalListener implementation

  /// <inheritdoc/>
  public void OnValueChanged(string signalName) {
    if (IsActive && !IsMarkedForCleanup) {
      CheckOperands();
    }
  }

  #endregion

  #region Implementation

  ParsingResult _parsingResult;
  BoolOperator _parsedExpression;
  List<SignalOperator> _registeredSignals;

  // Used by the RulesEditor dialog.
  internal static BoolOperator ParseAndValidate(
      string expression, AutomationBehavior behavior, out ParsingResult parsingResult, bool onlyCheck = false) {
    parsingResult = DependencyContainer.GetInstance<ExpressionParser>().Parse(expression, behavior);
    if (parsingResult.LastError != null) {
      if (!onlyCheck || parsingResult.LastScriptError is not ScriptError.BadStateError) {
        HostedDebugLog.Error(
            behavior, "Failed to parse condition: {0}\nError: {1}", expression, parsingResult.LastError);
      }
      return null;
    }
    if (parsingResult.ParsedExpression is not BoolOperator result) {
      HostedDebugLog.Error(behavior, "Expression is not a boolean operator: {0}", parsingResult.ParsedExpression);
      return null;
    }
    var hasSignals = false;
    result.VisitNodes(x => { hasSignals |= x is SignalOperator; });
    if (!hasSignals) {
      HostedDebugLog.Error(behavior, "Condition has no signals: {0}", expression);
      return null;
    }
    return result;
  }

  void ParseAndApply() {
    if (_parsingResult != default) {
      throw new InvalidOperationException("ParseAndApply should only be called once.");
    }
    ResetScriptError();
    _parsedExpression = ParseAndValidate(Expression, Behavior, out _parsingResult);
    if (_parsedExpression == null) {
      _lastScriptError = ParseErrorLocKey;
      Behavior.ReportError(this);
      return;
    }
    Behavior.IncrementStateVersion();
    Expression = _parsedExpression.Serialize();
    _registeredSignals = DependencyContainer.GetInstance<ScriptingService>().RegisterSignals(_parsedExpression, this);
    _canRunOnUnfinishedBuildings = _registeredSignals.Select(x => x.OnUnfinished).Aggregate((x, y) => x || y); 
  }

  bool CheckPrecondition(AutomationBehavior behavior) {
    if (string.IsNullOrEmpty(Precondition)) {
      return true;
    }
    var needLogs = Keyboard.current.ctrlKey.isPressed;
    var result = DependencyContainer.GetInstance<ExpressionParser>().Parse(Precondition, behavior);
    if (result.ParsedExpression == null) {
      if (result.LastScriptError is not ScriptError.BadStateError) {
        HostedDebugLog.Error(behavior, "Failed to parse precondition: {0}\nError: {1}", Precondition, result.LastError);
      } else if (needLogs) {
        HostedDebugLog.Info(behavior, "Precondition doesn't apply: {0}\nError: {1}", Precondition, result.LastError);
      }
      return false;
    }
    if (result.ParsedExpression is not BoolOperator boolOperatorExpr) {
      HostedDebugLog.Error(behavior, "Precondition is not a boolean operator: {0}", result.ParsedExpression);
      return false;
    }
    var resultValue = boolOperatorExpr.Execute();
    if (!resultValue && needLogs) {
      HostedDebugLog.Info(behavior, "Precondition didn't pass: {0}", Precondition);
    }
    return resultValue;
  }

  void CheckOperands() {
    bool newState;
    try {
      ResetScriptError();
      newState = _parsedExpression.Execute();
    } catch (ScriptError.RuntimeError e) {
      if (_lastScriptError != null) {
        throw;  // Can be already handled upstream in case of recursive calls.
      }
      ReportScriptError(e);
      throw;
    }
    if (ConditionState != newState) {
      Behavior.IncrementStateVersion();
    }
    ConditionState = newState;
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
