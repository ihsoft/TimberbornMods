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
    if (_parsedExpression != null) {
      CheckOperands();
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    ParseConditions();
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

  ParserContext _parserParserContext;
  BoolOperatorExpr _parsedExpression;

  void ParseConditions() {
    _parserParserContext = new ParserContext {
        OnSignalChanged = CheckOperands,
        ScriptHost = Behavior,
    };
    var res = Behavior.AutomationService.ExpressionParser.Parse(Expression, _parserParserContext);
    if (!res) {
      HostedDebugLog.Error(
          Behavior, "Failed to parse condition: {0}\nError: {1}", Expression, _parserParserContext.LastError);
      //FIXME: localize
      _uiDescription = TextColors.ColorizeText($"<RedHighlight>ERROR</RedHighlight>");
      return;
    }
    //FIXME: process expression to get the descirption
    _parsedExpression = _parserParserContext.ParsedExpression as BoolOperatorExpr;
    if (_parsedExpression == null) {
      HostedDebugLog.Error(
          Behavior, "Expression is not a boolean operator: {0}", _parserParserContext.ParsedExpression.Serialize());
      //FIXME: localize
      _uiDescription = TextColors.ColorizeText($"<RedHighlight>ERROR</RedHighlight>");
      return;
    }
    var description = ExpressionParser.Instance.GetDescription(_parserParserContext);
    _uiDescription = TextColors.ColorizeText($"<SolidHighlight>{description}</SolidHighlight>");
  }

  void Dispose() {
    _parserParserContext.Release();
    _parsedExpression = null;
  }

  void CheckOperands() {
    if (_parsedExpression != null) {
      ConditionState = _parsedExpression.Execute();
      
    } else {
      HostedDebugLog.Error(Behavior, "Signal change triggered, but the condition was broken: {0}", Expression);
    }
  }

  #endregion
}
