// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Timberborn.Common;
using Timberborn.Localization;

namespace IgorZ.Automation.ScriptingEngine.Parser;

/// <summary>Parser for the expressions in the scripting engine.</summary>
sealed class ExpressionParser {

  const string RuntimeErrorLocKey = "IgorZ.Automation.Scripting.Expressions.RuntimeError";

  #region API

  /// <summary>Current parser context.</summary>
  /// <remarks>It is set during parsing an expression or building a descriptions.</remarks>
  public ParserContext CurrentParserContext { get; private set; }

  /// <summary>Parses expression for the given context.</summary>
  public bool Parse(string input, ParserContext parserContext) {
    if (parserContext.ReferencedSignals.Count > 0 || parserContext.ParsedExpression != null) {
      throw new InvalidOperationException("Parser context is already in use");
    }
    try {
      CurrentParserContext = parserContext;
      CurrentParserContext.ParsedExpression = ProcessString(input);
      CurrentParserContext.LastError = null;
    } catch (ScriptError e) {
      CurrentParserContext.LastError = e.Message;
      CurrentParserContext.ParsedExpression = null;
      return false;
    } finally {
      CurrentParserContext = null;
    }
    return true;
  }

  /// <summary>Builds a human-readable description for the parsed expression.</summary>
  public string GetDescription(ParserContext parserContext) {
    if (parserContext.ParsedExpression == null) {
      throw new InvalidOperationException("Parser context is not initialized");
    }
    try {
      CurrentParserContext = parserContext;
      return CurrentParserContext.ParsedExpression.Describe();
    } catch (ScriptError e) {
      CurrentParserContext.LastError = e.Message;
      return Loc.T(RuntimeErrorLocKey);
    } finally {
      CurrentParserContext = null;
    }
  }

  #endregion

  #region Implementation

  static readonly Regex OperatorNameRegex = new(@"^\[a-zA-Z]+$");
  readonly ScriptingService _scriptingService;

  internal readonly ILoc Loc;
  internal static ExpressionParser Instance;

  ExpressionParser(ScriptingService scriptingService, ILoc loc) {
    _scriptingService = scriptingService;
    Loc = loc;
    Instance = this;
  }

  internal ActionDef GetActionDefinition(string actionName) {
    return _scriptingService.GetActionDefinition(actionName, CurrentParserContext.ScriptHost);
  }

  internal Action<ScriptValue[]> GetActionExecutor(string actionName) {
    return _scriptingService.GetActionExecutor(actionName, CurrentParserContext.ScriptHost);
  }

  internal SignalDef GetSignalDefinition(string signalName) {
    return _scriptingService.GetSignalDefinition(signalName, CurrentParserContext.ScriptHost);
  }

  internal Func<ScriptValue> GetSignalSource(string name) {
    CurrentParserContext.ReferencedSignals.Add(name);
    return _scriptingService.GetSignalSource(name, CurrentParserContext.ScriptHost);
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

  static IExpression ProcessString(string input) {
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

  static IExpression ReadFromTokens(Queue<string> tokens) {
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
      var result =
          BinaryOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? LogicalOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? SignalOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? ActionExpr.TryCreateFrom(operatorName, operands)
          ?? MathOperatorExpr.TryCreateFrom(operatorName, operands);
      if (result == null) {
        throw new ScriptError("Unknown operator: " + operatorName);
      }
      return result;
    }
    return ConstantValueExpr.TryCreateFrom(token) ?? new SymbolExpr { Value = token };
  }

  #endregion
}
