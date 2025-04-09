// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.TimberDev.UI;
using Timberborn.Common;
using Timberborn.Localization;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.Parser;

/// <summary>Parser for the expressions in the scripting engine.</summary>
sealed class ExpressionParser {

  const string RuntimeErrorLocKey = "IgorZ.Automation.Scripting.Expressions.RuntimeError";

  #region API

  /// <summary>Parses expression for the given context.</summary>
  public ParsingResult Parse(string input, AutomationBehavior scriptHost) {
    var parsingContext = new Context { ScriptHost = scriptHost, ScriptingService = _scriptingService };
    _contextStack.Push(parsingContext);
    try {
      if (input.Contains("{%")) {
        input = Preprocess(input);
      }
      var parsedExpression = ProcessString(input);
      return new ParsingResult {
          ParsedExpression = parsedExpression,
          ReferencedSignals = CurrentContext.ReferencedSignals.ToArray(),
          ReferencedActions = CurrentContext.ReferencedActions.ToArray(),
      };
    } catch (ScriptError e) {
      return new ParsingResult { LastScriptError = e };
    } finally {
      _contextStack.Pop();
    }
  }

  /// <summary>Gets a human-readable description for the parsed expression.</summary>
  public string GetDescription(IExpression expression, bool logErrors = false) {
    try {
      return CommonFormats.HighlightYellow(expression.Describe());
    } catch (ScriptError e) {
      if (logErrors) {
        DebugEx.Error("Failed to get description from: {0}\n{1}", expression.Serialize(), e);
      }
      return CommonFormats.HighlightRed(_loc.T(RuntimeErrorLocKey));
    }
  }

  #endregion

  #region Implementation

  public record Context {
    public AutomationBehavior ScriptHost { get; init; }
    public ScriptingService ScriptingService { get; init; }
    public HashSet<string> ReferencedSignals { get; } = [];
    public List<string> ReferencedActions { get; } = [];
    public bool IsPreprocessor { get; init; }
  }

  static readonly Regex OperatorNameRegex = new(@"^\[a-zA-Z]+$");

  readonly ScriptingService _scriptingService;
  readonly ILoc _loc;

  Context CurrentContext => _contextStack.Peek();
  readonly Stack<Context> _contextStack = new();

  ExpressionParser(ScriptingService scriptingService, ILoc loc) {
    _scriptingService = scriptingService;
    _loc = loc;
  }

  string Preprocess(string input) {
    var evaluator = new MatchEvaluator(PreprocessorMatcher);
    return Regex.Replace(input, @"\{%([^%]*)%}", evaluator);
  }

  string PreprocessorMatcher(Match match) {
    var expression = match.Groups[1].Value;
    _contextStack.Push(CurrentContext with { IsPreprocessor = true });
    try {
      var parsedExpression = ProcessString(expression);
      if (parsedExpression is BinaryOperatorExpr binaryOperatorExpr) {
        if (!binaryOperatorExpr.Execute()) {
          throw new ScriptError("Preprocessor: Expression is not true: " + expression);
        }
        return "";
      }
      if (parsedExpression is not IValueExpr valueExpr) {
        throw new ScriptError("Preprocessor: Not a value expression: " + expression);
      }
      var value = valueExpr.ValueFn();
      return value.ValueType switch {
          ScriptValue.TypeEnum.String => value.AsString,
          ScriptValue.TypeEnum.Number => value.AsNumber.ToString(),
          _ => throw new InvalidOperationException("Unsupported type: " + value.ValueType),
      };
    } finally {
      _contextStack.Pop();
    }
  }

  static Queue<string> Tokenize(string input) {
    if (input == null) {
      throw new ArgumentNullException(nameof(input));
    }
    var currentPos = 0;
    var tokens = new Queue<string>();
    while (currentPos < input.Length) {
      // Skip the leading spaces.
      if (input[currentPos] == ' ') {
        while (currentPos < input.Length && input[currentPos] == ' ') {
          currentPos++;
        }
        continue;
      }

      // Capture the string literal.
      if (input[currentPos] == '\'') {
        var literalEndPos = input.IndexOf('\'', currentPos + 1);
        if (literalEndPos == -1) {
          throw new ScriptError("Unmatched quote");
        }
        tokens.Enqueue(input.Substring(currentPos, literalEndPos - currentPos + 1));
        currentPos = literalEndPos + 1;
        continue;
      }

      // Capture the statement separator.
      if (input[currentPos] == '(' || input[currentPos] == ')') {
        tokens.Enqueue(input.Substring(currentPos, 1));
        currentPos++;
        continue;
      }
      
      // Capture the token.
      var tokenEndPos = currentPos + 1;
      while (tokenEndPos < input.Length && input[tokenEndPos] != ' ' && input[tokenEndPos] != '(' && input[tokenEndPos] != ')') {
        tokenEndPos++;
      }
      tokens.Enqueue(input.Substring(currentPos, tokenEndPos - currentPos));
      currentPos = tokenEndPos;
    }
    return tokens;
  }

  IExpression ProcessString(string input) {
    var tokens = Tokenize(input);
    var result = ReadFromTokens(tokens);
    if (tokens.Any()) {
      throw new ScriptError("Unexpected token at the end of the expression: " + tokens.Peek());
    }
    return result;
  }

  static void CheckHasMoreTokens(Queue<string> tokens) {
    if (tokens.IsEmpty()) {
      throw new ScriptError("Unexpected EOF while reading expression");
    }
  }

  IExpression ReadFromTokens(Queue<string> tokens) {
    if (!tokens.Any()) {
      throw new ScriptError("Unexpected EOF while reading expression");
    }
    var token = tokens.Dequeue();
    if (token == "(") {
      CheckHasMoreTokens(tokens);
      var operatorName = tokens.Dequeue();
      if (OperatorNameRegex.IsMatch(operatorName)) {
        throw new ScriptError("Bad operator name: " + operatorName);
      }
      CheckHasMoreTokens(tokens);
      var operands = new List<IExpression>();
      while (tokens.Peek() != ")") {
        operands.Add(ReadFromTokens(tokens));
        CheckHasMoreTokens(tokens);
      }
      if (operands.Count == 0) {
        throw new ScriptError("Empty operator expression");
      }
      tokens.Dequeue(); // ")"

      // The sequence below should be ordered by the frequency of the usage. The operators that are more likely to be
      // used in the game should come first.
      var result =
          BinaryOperatorExpr.TryCreateFrom(CurrentContext, operatorName, operands)
          ?? SignalOperatorExpr.TryCreateFrom(CurrentContext, operatorName, operands)
          ?? ActionExpr.TryCreateFrom(CurrentContext, operatorName, operands)
          ?? LogicalOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? MathOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? GetPropertyOperatorExpr.TryCreateFrom(CurrentContext, operatorName, operands);
      if (result == null) {
        throw new ScriptError("Unknown operator: " + operatorName);
      }
      return result;
    }
    return ConstantValueExpr.TryCreateFrom(token) ?? new SymbolExpr { Value = token };
  }

  #endregion
}
