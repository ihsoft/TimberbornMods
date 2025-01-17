// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class ConstantValueExpr : IExpression, IValueExpr {
  public IValueExpr.ValueType Type { get; private init; }
  public Func<string> GetStringValue { get; private init; } = () => throw new ScriptError("Not a string value");
  public Func<int> GetNumberValue { get; private init; } = () => throw new ScriptError("Not a number value");

  public static IExpression TryCreateFrom(string token) {
    if (token.StartsWith("'")) {
      var literal = token.Substring(1, token.Length - 2);
      return new ConstantValueExpr { Type = IValueExpr.ValueType.String, GetStringValue = () => literal };
    }
    if (token[0] >= '0' && token[0] <= '9') {
      if (!int.TryParse(token, out var number)) {
        throw new ScriptError($"Invalid number literal: {token}");
      }
      return new ConstantValueExpr { Type = IValueExpr.ValueType.Number, GetNumberValue = () => number };
    }
    return null;
  }
}
