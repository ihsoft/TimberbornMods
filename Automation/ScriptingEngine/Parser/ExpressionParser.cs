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
  public void Parse(string input, ParserContext parserContext) {
    if (parserContext.ScriptHost == null) {
      throw new InvalidOperationException("Script host not set");
    }
    if (parserContext.ReferencedSignals.Count > 0 || parserContext.ParsedExpression != null) {
      throw new InvalidOperationException("Parser context is already in use");
    }
    _parsingContext = new Context { ScriptHost = parserContext.ScriptHost, ScriptingService = _scriptingService };
    _contextStack.Push(parserContext);
    parserContext.LastError = null;
    try {
      if (input.Contains("{%")) {
        input = Preprocess(input);
      }
      parserContext.ParsedExpression = ProcessString(input);
    } catch (ScriptError e) {
      parserContext.LastError = e.Message;
    }
    _contextStack.Pop();
    _parsingContext = null;
  }

  /// <summary>Gets a human-readable description for the parsed expression.</summary>
  public string GetDescription(IExpression expression, bool logErrors = false) {
    try {
      return CommonFormats.HighlightYellow(expression.Describe());
    } catch (ScriptError e) {
      if (logErrors) {
        DebugEx.Error("Failed to get description from: {0}\n{1}", expression.Serialize(), e);
      }
      return CommonFormats.HighlightRed(Loc.T(RuntimeErrorLocKey));
    }
  }

  #endregion

  #region Implementation

  public record Context {
    public AutomationBehavior ScriptHost { get; init; }
    public ScriptingService ScriptingService { get; init; }
  }

  static readonly Regex OperatorNameRegex = new(@"^\[a-zA-Z]+$");
  readonly ScriptingService _scriptingService;

  internal readonly ILoc Loc;
  internal static ExpressionParser Instance;

  ParserContext CurrentParserContext => _contextStack.Peek();
  readonly Stack<ParserContext> _contextStack = new();

  Context _parsingContext;

  ExpressionParser(ScriptingService scriptingService, ILoc loc) {
    _scriptingService = scriptingService;
    Loc = loc;
    Instance = this;
  }

  string Preprocess(string input) {
    _contextStack.Push(new ParserContext {
        ScriptHost = CurrentParserContext.ScriptHost,
    });
    try {
      var evaluator = new MatchEvaluator(PreprocessorMatcher);
      return Regex.Replace(input, @"\{%([^%]*)%}", evaluator);
    } finally {
      _contextStack.Pop();
    }
  }

  string PreprocessorMatcher(Match match) {
    var expression = match.Groups[1].Value;
    var context = new ParserContext {
        ScriptHost = CurrentParserContext.ScriptHost,
    };
    _contextStack.Push(context);
    try {
      var parsedExpression = ProcessString(expression);
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
      var signal = SignalOperatorExpr.TryCreateFrom(_parsingContext, operatorName, operands);
      if (signal != null) {
        CurrentParserContext.ReferencedSignals.Add(signal.SignalName);
        return signal;
      }
      var result =
          BinaryOperatorExpr.TryCreateFrom(_parsingContext, operatorName, operands)
          ?? ActionExpr.TryCreateFrom(_parsingContext, operatorName, operands)
          ?? LogicalOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? MathOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? GetPropertyOperatorExpr.TryCreateFrom(_parsingContext, operatorName, operands);
      if (result == null) {
        throw new ScriptError("Unknown operator: " + operatorName);
      }
      return result;
    }
    return ConstantValueExpr.TryCreateFrom(token) ?? new SymbolExpr { Value = token };
  }

  #endregion
}
