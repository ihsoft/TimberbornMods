// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.Automation.UI;
using Timberborn.Common;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class ExpressionParser {

  static readonly Regex OperatorNameRegex = new(@"^\[a-zA-Z]+$");

  public IExpression ParsedExpression { get; private set; }
  public string Source { get; private set; }

  public void Parse(string input) {
    ParsedExpression = ReadFromTokens(Tokenize(input));
  }

  Queue<string> Tokenize(string input) {
    if (input == null) {
      throw new ArgumentNullException(nameof(input));
    }
    Source = input.Replace("(", " ( ").Replace(")", " ) ");
    var tokens = Regex.Matches(Source, "['].+?[']|[^ ]+")
        .Cast<Match>()
        .Select(m => m.Value);
    return new Queue<string>(tokens);
  }

  IExpression ReadFromTokens(Queue<string> tokens) {
    if (!tokens.Any()) {
      //FIXME: one day provide position.
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
          ?? SignalOperatorExpr.TryCreate(operatorName, operands);
      if (result == null) {
        throw new ScriptError("Unknown operator: " + operatorName);
      }
      return result;
    }
    if (token == ")") {
      throw new ScriptError("Unexpected ')' while reading expression");
    }
    if (token[0] >= '0' && token[0] <= '9') {
      return ValueExpr.CreateNumberConstant(token);
    }
    if (token.StartsWith("'")) {
      return ValueExpr.CreateStringConstant(token);
    }
    return new SymbolExpr { Value = token };
  }
}
