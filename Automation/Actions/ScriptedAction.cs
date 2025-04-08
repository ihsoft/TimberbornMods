// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
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
    return new ScriptedAction {
        TemplateFamily = TemplateFamily, Expression = Expression, CleanupAction = CleanupAction,
    };
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
    var result = DependencyContainer.GetInstance<ExpressionParser>().Parse(Expression, behavior);
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

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    base.OnBehaviorToBeCleared();
    if (!string.IsNullOrEmpty(CleanupAction)) {
      try {
        ParseAction(CleanupAction).Execute();
      } catch (ScriptError e) {
        HostedDebugLog.Error(Behavior, "Cleanup action failed: {0}\nError: {1}", CleanupAction, e.Message);
      }
    }
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

  /// <summary>Action to execute when the rule is being cleaned up.</summary>
  /// <remarks>Use it to restore the building state when needed.</remarks>
  public string CleanupAction { get; private set; }

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
  static readonly PropertyKey<string> CleanupActionKey = new("CleanupAction");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    Expression = objectLoader.Get(ExpressionKey);
    CleanupAction = objectLoader.GetValueOrDefault(CleanupActionKey, null);
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ExpressionKey, Expression);
    if (!string.IsNullOrEmpty(CleanupAction)) {
      objectSaver.Set(CleanupActionKey, CleanupAction);
    }
  }

  #endregion

  #region Implementation

  ActionExpr _parsedExpression;

  void ParseAndApply() {
    _uiDescription = null;
    _parsedExpression = ParseAction(Expression);
    if (_parsedExpression != null) {
      Expression = _parsedExpression!.Serialize();
    } else {
      _uiDescription = CommonFormats.HighlightRed(Behavior.Loc.T(ParseErrorLocKey));
    }
  }

  ActionExpr ParseAction(string expression) {
    var parsingResult = DependencyContainer.GetInstance<ExpressionParser>().Parse(expression, Behavior);
    if (parsingResult.LastError != null) {
      HostedDebugLog.Error(Behavior, "Failed to parse action: {0}\nError: {1}", expression, parsingResult.LastError);
      return null;
    }
    if (parsingResult.ParsedExpression is not ActionExpr parsedExpression) {
      HostedDebugLog.Error(Behavior, "Expression is not an action operator: {0}", parsingResult.ParsedExpression);
      return null;
    }
    return parsedExpression;
  }

  #endregion
}
