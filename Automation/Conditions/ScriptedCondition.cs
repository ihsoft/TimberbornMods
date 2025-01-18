// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.Localization;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.Conditions;

sealed class ScriptedCondition : AutomationConditionBase {

  const string AndOperatorLocString = "IgorZ.Automation.Conditions.AndOperator";

  #region AutomationConditionBase overrides

  /// <inheritdoc/>
  public override string UiDescription => _uiDescription;
  string _uiDescription;

  /// <inheritdoc/>
  public override void SyncState() {
    CheckOperands();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    if (!ParseConditions()) {
      HostedDebugLog.Error(Behavior, "Failed to parse conditions: {0}", _parserContext.LastError);
      IsMarkedForCleanup = true;
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    Dispose();
  }

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new ScriptedCondition { Expression = Expression };
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    //FIXME: somehow check condition?
    return true;
  }

  #endregion

  #region API

  /// <summary>Script code for expression to check.</summary>
  /// <remarks>
  /// It must abe a boolean operator. See <see cref="BoolOperatorExpr"/> for the list of conditions. Example of a
  /// condition: (and (eq (sig Weather.Season) 'drought') (gt Floodgate.Height 0.5)). 
  /// </remarks>
  // ReSharper disable once MemberCanBePrivate.Global
  public string Expression { get; private set; }

  /// <summary>Sets the expression conditions.</summary>
  /// <remarks>Can only be set on the non-active condition.</remarks>
  /// <seealso cref="Conditions"/>
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

  ExpressionParser.Context _parserContext;
  BoolOperatorExpr _parsedExpression;

  bool ParseConditions() {
    _parserContext = new ExpressionParser.Context();
    _parserContext.OnSignalChanged = CheckOperands;
    //FIXME: inject it.
    var parser = new ExpressionParser(ScriptingService.Instance, Behavior);
    var res = parser.Parse(Expression, _parserContext);
    //FIXME: process expression to get the descirption
    _uiDescription = TextColors.ColorizeText($"<SolidHighlight>{Expression}</SolidHighlight>");
    if (res) {
      _parsedExpression = _parserContext.ParsedExpression as BoolOperatorExpr;
      if (_parsedExpression == null) {
        HostedDebugLog.Error(Behavior, "Expression is not a boolean operator: {0}", Expression);
        _uiDescription = TextColors.ColorizeText($"<RedHighlight>ERROR</RedHighlight>: ") + _uiDescription;
        res = false;
      }
    }
    return res;
  }

  void Dispose() {
    _parserContext.Release();
    _parsedExpression = null;
  }

  void CheckOperands() {
    ConditionState = _parsedExpression.Execute();
  }

  #endregion
}
