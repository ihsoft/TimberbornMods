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

class ExpressionParser {

  public bool Parse(string input, ParserContext parserContext) {
    if (parserContext.ReferencedSignals.Count > 0 || parserContext.ParsedExpression != null) {
      throw new InvalidOperationException("Parser context is already in use");
    }
    try {
      _currentParserContext = parserContext;
      _currentParserContext.ParsedExpression = ReadFromTokens(Tokenize(input));
      _currentParserContext.LastError = null;
    } catch (ScriptError e) {
      _currentParserContext.LastError = e.Message;
      _currentParserContext.ParsedExpression = null;
      return false;
    } finally {
      _currentParserContext = null;
    }
    return true;
  }

  public string GetDescription(ParserContext parserContext) {
    if (parserContext.ParsedExpression == null) {
      throw new InvalidOperationException("Parser context is not initialized");
    }
    try {
      _currentParserContext = parserContext;
      return _currentParserContext.ParsedExpression.Describe();
    } finally {
      _currentParserContext = null;
    }
  }

  static readonly Regex OperatorNameRegex = new(@"^\[a-zA-Z]+$");
  ParserContext _currentParserContext;

  readonly ScriptingService _scriptingService;

  internal readonly ILoc Loc;
  internal static ExpressionParser Instance;

  ExpressionParser(ScriptingService scriptingService, ILoc loc) {
    _scriptingService = scriptingService;
    Loc = loc;
    Instance = this;
  }

  internal ActionDef GetActionDefinition(string actionName) {
    return _scriptingService.GetActionDefinition(actionName, _currentParserContext.ScriptHost);
  }

  internal Action<ScriptValue[]> GetActionExecutor(string actionName) {
    return _scriptingService.GetActionExecutor(actionName, _currentParserContext.ScriptHost);
  }

  internal SignalDef GetSignalDefinition(string signalName) {
    return _scriptingService.GetSignalDefinition(signalName, _currentParserContext.ScriptHost);
  }

  internal Func<ScriptValue> GetSignalSource(string name) {
    _currentParserContext.ReferencedSignals.Add(name);
    return _scriptingService.GetSignalSource(name, _currentParserContext.ScriptHost);
  }

  Queue<string> Tokenize(string input) {
    if (input == null) {
      throw new ArgumentNullException(nameof(input));
    }
    var source = input.Replace("(", " ( ").Replace(")", " ) ");
    var tokens = Regex.Matches(source, "['].+?[']|[^ ]+")
        .Cast<Match>()
        .Select(m => m.Value);
    return new Queue<string>(tokens);
  }

  IExpression ReadFromTokens(Queue<string> tokens) {
    if (!tokens.Any()) {
      throw new ScriptError("Unexpected EOF while reading expression");
    }
    var token = tokens.Dequeue();
    if (token == "(") {
      if (tokens.IsEmpty()) {
        throw new ScriptError("Unexpected EOF while reading expression");
      }
      var operatorName = tokens.Dequeue();
      if (OperatorNameRegex.IsMatch(operatorName)) {
        throw new ScriptError("Bad operator name: " + operatorName);
      }
      var operands = new List<IExpression>();
      while (tokens.Peek() != ")") {
        operands.Add(ReadFromTokens(tokens));
      }
      if (operands.Count == 0) {
        throw new ScriptError("Empty operator expression");
      }
      tokens.Dequeue(); // ")"
      var result =
          BinaryOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? LogicalOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? SignalOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? ActionExpr.TryCreateFrom(operatorName, operands);
      if (result == null) {
        throw new ScriptError("Unknown operator: " + operatorName);
      }
      return result;
    }
    if (token == ")") {
      throw new ScriptError("Unexpected ')' while reading expression");
    }
    return ConstantValueExpr.TryCreateFrom(token) ?? new SymbolExpr { Value = token };
  }
}
