// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Timberborn.Common;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class ExpressionParser(ScriptingService scriptingService) {

  public bool Parse(string input, ParserContext parserContext) {
    if (parserContext.SignalSources.Count > 0 || parserContext.ParsedExpression != null) {
      throw new InvalidOperationException("Context is already in use");
    }
    try {
      _currentParserContext = parserContext;
      _currentParserContext.ParsedExpression = ReadFromTokens(Tokenize(input));
      _currentParserContext.LastError = null;
    } catch (ScriptError e) {
      _currentParserContext.LastError = e.Message;
      _currentParserContext.ParsedExpression = null;
      _currentParserContext.Release();
      return false;
    } finally {
      _currentParserContext = null;
    }
    return true;
  }

  static readonly Regex OperatorNameRegex = new(@"^\[a-zA-Z]+$");
  ParserContext _currentParserContext;

  internal ActionDef GetActionDefinition(string actionName) {
    return scriptingService.GetActionDefinition(actionName, _currentParserContext.ScriptHost);
  }

  internal Action<ScriptValue[]> GetAction(string actionName) {
    return scriptingService.GetActionExecutor(actionName, _currentParserContext.ScriptHost);
  }

  internal ITriggerSource GetSignalSource(string name) {
    if (!_currentParserContext.SignalSources.TryGetValue(name, out var source)) {
      source = scriptingService.GetTriggerSource(
          name, _currentParserContext.ScriptHost, _currentParserContext.OnSignalChanged);
      _currentParserContext.SignalSources[name] = source;
    }
    return source;
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
          ?? SignalOperatorExpr.TryCreateFrom(operatorName, operands, this)
          ?? ActionExpr.TryCreateFrom(operatorName, operands, this);
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
