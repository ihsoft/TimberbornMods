// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Text.RegularExpressions;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;

namespace IgorZ.Automation.ScriptingEngine.Parser;

abstract class ParserBase {
  public record Context {
    public AutomationBehavior ScriptHost { get; init; }
    public ScriptingService ScriptingService { get; init; }
  }

  /// <summary>Parses expression for the given context.</summary>
  public ParsingResult Parse(string input, AutomationBehavior scriptHost) {
    CurrentContext = new Context { ScriptHost = scriptHost, ScriptingService = _scriptingService };
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
    }
  }

  /// <summary>Processes the string input into an expression.</summary>
  protected abstract IExpression ProcessString(string input);

  protected Context CurrentContext { get; private set; }
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
    if (parsedExpression is BinaryOperator binaryOperatorExpr) {
      if (!binaryOperatorExpr.Execute()) {
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
}
