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
using UnityEngine.InputSystem;

namespace IgorZ.Automation.Conditions;

sealed class ScriptedCondition : AutomationConditionBase, ISignalListener {

  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";
  const string RuntimeErrorLocKey = "IgorZ.Automation.Scripting.Expressions.RuntimeError";

  #region AutomationConditionBase overrides

  /// <inheritdoc/>
  public override bool CanRunOnUnfinishedBuildings => _canRunOnUnfinishedBuildings;
  bool _canRunOnUnfinishedBuildings;

  /// <inheritdoc/>
  public override string UiDescription {
    get {
      if (_staticDescription != null) {
        return _staticDescription;
      }
      var description = DependencyContainer.GetInstance<ExpressionParser>().GetDescription(_parsedExpression);
      return ConditionState ? CommonFormats.HighlightGreen(description) : CommonFormats.HighlightYellow(description);
    }
  }
  string _staticDescription;

  /// <inheritdoc/>
  public override void SyncState(bool force) {
    base.SyncState(force);
    if (_parsedExpression == null) {
      return;  // The condition is broken, no need to sync.
    }
    try {
      if (force || ConditionState != _parsedExpression.Execute()) {
        OnValueChanged(null);
      }
    } catch (ScriptError e) {
      HostedDebugLog.Error(Behavior, "SyncState failed: {0}", e.Message);
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    ParseAndApply();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    if (_registeredSignals != null) {
      var scriptingService = DependencyContainer.GetInstance<ScriptingService>();
      scriptingService.UnregisterSignals(_registeredSignals, this);
    }
    if (_parsedExpression == null) {
      Behavior.ClearError(this);
    }
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
      _lastValidationResult = ParseAndValidate(Expression, behavior, out _) != null;
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
  /// condition: "(and (eq (sig Weather.Season) 'drought') (gt Floodgate.Height 0.5))".
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

  #endregion

  #region IGameSerializable implemenation

  static readonly PropertyKey<string> ExpressionKey = new("Expression");
  static readonly PropertyKey<string> PreconditionKey = new("Precondition");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    Expression = objectLoader.Get(ExpressionKey);
    Precondition = objectLoader.GetValueOrDefault(PreconditionKey, null);
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ExpressionKey, Expression);
    if (!string.IsNullOrEmpty(Precondition)) {
      objectSaver.Set(PreconditionKey, Precondition);
    }
  }

  #endregion

  #region ISignalListener implementation

  /// <inheritdoc/>
  public void OnValueChanged(string signalName) {
    if (IsActive) {
      CheckOperands(signalName);
    }
  }

  #endregion

  #region Implementation

  ParsingResult _parsingResult;
  BoolOperator _parsedExpression;
  List<SignalOperator> _registeredSignals;
  readonly HashSet<string> _oneShotSignals = [];

  // Used by the RulesEditor dialog.
  internal static BoolOperator ParseAndValidate(
      string expression, AutomationBehavior behavior, out ParsingResult parsingResult) {
    parsingResult = DependencyContainer.GetInstance<ExpressionParser>().Parse(expression, behavior);
    if (parsingResult.LastError != null) {
      HostedDebugLog.Error(behavior, "Failed to parse condition: {0}\nError: {1}", expression, parsingResult.LastError);
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
    _parsedExpression = ParseAndValidate(Expression, Behavior, out _parsingResult);
    if (_parsedExpression == null) {
      _staticDescription = CommonFormats.HighlightRed(Behavior.Loc.T(ParseErrorLocKey));
      Behavior.ReportError(this);
      return;
    }
    Behavior.IncrementStateVersion();
    Expression = _parsedExpression.Serialize();
    _registeredSignals = DependencyContainer.GetInstance<ScriptingService>().RegisterSignals(_parsedExpression, this);
    foreach (var signal in _registeredSignals) {
      _canRunOnUnfinishedBuildings |= signal.OnUnfinished;
      if (signal.OneShot) {
        _oneShotSignals.Add(signal.SignalName);
      }
    }
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

  void CheckOperands(string signalName) {
    if (!Behavior.BlockObject.IsFinished && !CanRunOnUnfinishedBuildings) {
      return;
    }
    if (_parsedExpression == null) {
      HostedDebugLog.Error(Behavior, "Signal change triggered, but the condition was broken: {0}", Expression);
      return;
    }
    bool newState;
    try {
      newState = _parsedExpression.Execute();
    } catch (ScriptError) {
      if (_parsedExpression == null) {
        throw;  // Can be already handled upstream in case of recursive calls.
      }
      _parsedExpression = null;
      _staticDescription = CommonFormats.HighlightRed(Behavior.Loc.T(RuntimeErrorLocKey));
      Behavior.ReportError(this);
      throw;
    }
    if (ConditionState != newState) {
      Behavior.IncrementStateVersion();
    }
    ConditionState = newState;
    if (signalName != null && _oneShotSignals.Contains(signalName)) {
      HostedDebugLog.Fine(Behavior, "OneShot signal '{0}' triggered. Cleanup the rule: {1}", signalName, Expression);
      IsMarkedForCleanup = true;
    }
  }

  #endregion
}
