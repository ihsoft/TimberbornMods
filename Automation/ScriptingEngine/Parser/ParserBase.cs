// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Token = IgorZ.Automation.ScriptingEngine.Parser.TokenizerBase.Token;

namespace IgorZ.Automation.ScriptingEngine.Parser;

abstract class ParserBase {

  #region API

  /// <summary>Parses expression for the given context.</summary>
  public ParsingResult Parse(string input, AutomationBehavior scriptHost) {
    CurrentContext = new ExpressionContext { ScriptHost = scriptHost };
    try {
      if (input.Contains("{%")) {
        input = Preprocess(input);
      }
      var parsedExpression = ProcessString(input);
      return new ParsingResult {
          ParsedExpression = parsedExpression,
      };
    } catch (ScriptError e) {
      return new ParsingResult { LastScriptError = e };
    } finally {
      CurrentContext = null;
    }
  }

  /// <summary>Serializes expression back to the parsable form.</summary>
  public abstract string Decompile(IExpression expression);

  /// <summary>Inverts the condition expression.</summary>
  /// <remarks>
  /// Not simply negates the result, but rebuilds the expression so that it returns the inversed result.
  /// </remarks>
  public BooleanOperator InvertBooleanExpression(BooleanOperator booleanOperator, AutomationBehavior scriptHost) {
    CurrentContext = new ExpressionContext { ScriptHost = scriptHost };
    try {
      return InvertBooleanExpressionInternal(booleanOperator);
    } finally {
      CurrentContext = null;
    }
  }

  #endregion

  #region Inherited

  /// <summary>Processes the string input into an expression.</summary>
  protected abstract IExpression ProcessString(string input);

  protected ExpressionContext CurrentContext { get; private set; }

  protected static void AssertNumberOfOperandsExact(Token token, IList<IExpression> arguments, int expected) {
    var count = arguments.Count;
    if (expected != count) {
      throw new ScriptError.ParsingError(token, $"Expected exactly {expected} arguments, but got {count}");
    }
  }

  protected static string GetSymbolValue(Token token, IList<IExpression> operands, int index) {
    var operand = operands[index];
    if (operand is not SymbolExpr symbolExpr) {
      throw new ScriptError.ParsingError(token, $"Expected symbol at position #{index + 1}, but got: {operand}");
    }
    return symbolExpr.Value;
  }

  protected static string UnwrapStringLiteralExpr(Token token, IList<IExpression> expressions, int index) {
    if (index >= expressions.Count) {
      throw new ScriptError.ParsingError(token, $"Not enough arguments");
    }
    return expressions[index] is ConstantValueExpr { ValueType: ScriptValue.TypeEnum.String } constantValueExpr
        ? constantValueExpr.ValueFn().AsString
        : throw new ScriptError.ParsingError(token, $"Expected string literal at position {index + 1}");
  }

  #endregion

  #region Implementation

  ScriptingService _scriptingService;

  [Inject]
  public void InjectDependencies(ScriptingService scriptingService) {
    _scriptingService = scriptingService;
  }

  string Preprocess(string input) {
    var evaluator = new MatchEvaluator(PreprocessorMatcher);
    return Regex.Replace(input, @"\{%([^%]*)%}", evaluator);
  }

  string PreprocessorMatcher(Match match) {
    var expression = match.Groups[1].Value;
    var parsedExpression = ProcessString(expression);
    if (parsedExpression is ComparisonOperator comparisonOperator) {
      if (!comparisonOperator.Execute()) {
        throw new ScriptError.BadStateError(
            CurrentContext.ScriptHost, "Preprocessor expression is not true: " + expression);
      }
      return "";
    }
    if (parsedExpression is not IValueExpr valueExpr) {
      throw new ScriptError.ParsingError("Not a value expression: " + expression);
    }
    var value = valueExpr.ValueFn();
    return value.ValueType switch {
        ScriptValue.TypeEnum.String => value.AsString,
        ScriptValue.TypeEnum.Number => value.AsNumber.ToString(),
        _ => throw new InvalidOperationException("Unsupported type: " + value.ValueType),
    };
  }

  BooleanOperator InvertBooleanExpressionInternal(BooleanOperator booleanOperator) {
    if (booleanOperator is LogicalOperator logicalOperator) {
      if (logicalOperator.OperatorType == LogicalOperator.OpType.Not) {
        return logicalOperator.Operands[0] as BooleanOperator;
      }
      var invertedOperands = logicalOperator.Operands.Cast<BooleanOperator>().Select(InvertBooleanExpressionInternal)
          .ToArray<IExpression>();
      return logicalOperator.OperatorType == LogicalOperator.OpType.And
          ? LogicalOperator.CreateOr(invertedOperands)
          : LogicalOperator.CreateAnd(invertedOperands);
    }
    if (booleanOperator is ComparisonOperator comparisonOperator) {
      return comparisonOperator.OperatorType switch {
          ComparisonOperator.OpType.Equal =>
              ComparisonOperator.CreateNe(CurrentContext, comparisonOperator.Operands),
          ComparisonOperator.OpType.NotEqual =>
              ComparisonOperator.CreateEq(CurrentContext, comparisonOperator.Operands),
          ComparisonOperator.OpType.GreaterThan =>
              ComparisonOperator.CreateLe(CurrentContext, comparisonOperator.Operands),
          ComparisonOperator.OpType.GreaterThanOrEqual =>
              ComparisonOperator.CreateLt(CurrentContext, comparisonOperator.Operands),
          ComparisonOperator.OpType.LessThan =>
              ComparisonOperator.CreateGe(CurrentContext, comparisonOperator.Operands),
          ComparisonOperator.OpType.LessThanOrEqual =>
              ComparisonOperator.CreateGt(CurrentContext, comparisonOperator.Operands),
          _ => throw new InvalidOperationException($"Unexpected comparison operator {comparisonOperator}"),
      };
    }
    throw new InvalidOperationException($"Unsupported boolean operator: {booleanOperator}");
  }

  #endregion
}
