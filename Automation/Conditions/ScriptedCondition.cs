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

namespace IgorZ.Automation.Conditions;

sealed class ScriptedCondition : AutomationConditionBase {

  const string ParseErrorLocKey = "IgorZ.Automation.Scripting.Expressions.ParseError";
  const string RuntimeErrorLocKey = "IgorZ.Automation.Scripting.Expressions.RuntimeError";

  #region AutomationConditionBase overrides

  /// <inheritdoc/>
  public override string UiDescription => _uiDescription
      ?? CommonFormats.HighlightYellow(
          DependencyContainer.GetInstance<ExpressionParser>().GetDescription(_parsedExpression));
  string _uiDescription;

  /// <inheritdoc/>
  public override void SyncState() {
    if (_parsedExpression != null) {
      CheckOperands();
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    ParseAndApply();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    foreach (var signal in _parsingResult.ReferencedSignals) {
      DependencyContainer.GetInstance<ScriptingService>()
          .UnregisterSignalChangeCallback(signal, Behavior, CheckOperands);
    }
  }

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new ScriptedCondition { Expression = Expression };
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    if (_lastValidatedBehavior == behavior) {
      return _lastValidationResult;
    }
    _lastValidatedBehavior = behavior;
    var result = DependencyContainer.GetInstance<ExpressionParser>().Parse(Expression, behavior);
    _lastValidationResult = result.ParsedExpression != null;
    if (!_lastValidationResult && Keyboard.current.ctrlKey.isPressed) {
      HostedDebugLog.Warning(behavior, "Validation didn't pass: {0}\n{1}", Expression, result.LastScriptError);
    }
    return _lastValidationResult;
  }

  AutomationBehavior _lastValidatedBehavior;
  bool _lastValidationResult;

  #endregion

  #region API

  /// <summary>Script code for expression to check.</summary>
  /// <remarks>
  /// It must be a boolean operator. See <see cref="BoolOperatorExpr"/> for the list of conditions. Example of a
  /// condition: "(and (eq (sig Weather.Season) 'drought') (gt Floodgate.Height 0.5))". 
  /// </remarks>
  // ReSharper disable once MemberCanBePrivate.Global
  public string Expression { get; private set; }

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
  BoolOperatorExpr _parsedExpression;

  void ParseAndApply() {
    _uiDescription = null;
    _parsingResult = DependencyContainer.GetInstance<ExpressionParser>().Parse(Expression, Behavior);
    if (_parsingResult.LastError != null) {
      HostedDebugLog.Error(
          Behavior, "Failed to parse condition: {0}\nError: {1}", Expression, _parsingResult.LastError);
      _uiDescription = CommonFormats.HighlightRed(Behavior.Loc.T(ParseErrorLocKey));
      return;
    }
    _parsedExpression = _parsingResult.ParsedExpression as BoolOperatorExpr;
    if (_parsedExpression == null) {
      HostedDebugLog.Error(
          Behavior, "Expression is not a boolean operator: {0}", _parsingResult.ParsedExpression.Serialize());
      _uiDescription = CommonFormats.HighlightRed(Behavior.Loc.T(ParseErrorLocKey));
      return;
    }
    if (_parsingResult.ReferencedSignals.Length == 0) {
      HostedDebugLog.Error(Behavior, "Condition has no signals: {0}", Expression);
      _uiDescription = CommonFormats.HighlightRed(Behavior.Loc.T(ParseErrorLocKey));
      return;
    }

    foreach (var signal in _parsingResult.ReferencedSignals) {
      DependencyContainer.GetInstance<ScriptingService>()
          .RegisterSignalChangeCallback(signal, Behavior, CheckOperands);
    }
    Expression = _parsedExpression.Serialize();
  }

  void CheckOperands() {
    if (!Behavior.BlockObject.IsFinished) {
      return;
    }
    if (_parsedExpression == null) {
      HostedDebugLog.Error(Behavior, "Signal change triggered, but the condition was broken: {0}", Expression);
      return;
    }
    try {
      ConditionState = _parsedExpression.Execute();
    } catch (ExecutionInterrupted e) {
      HostedDebugLog.Fine(Behavior, "Condition execution interrupted: {0}\nReason: {1}", Expression, e.Reason);
    } catch (ScriptError e) {
      HostedDebugLog.Error(Behavior, "Error in condition execution: {0}\nReason: {1}", Expression, e.Message);
      _parsedExpression = null;
      _uiDescription = Behavior.Loc.T(RuntimeErrorLocKey);
    }
  }

  #endregion
}
