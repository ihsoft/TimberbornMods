// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class ConstantValueExpr : IExpression, IValueExpr {
  public ScriptValue.ValueType Type { get; private init; }
  public Func<ScriptValue> ValueFn { get; private init; }

  public static IExpression TryCreateFrom(string token) {
    if (token.StartsWith("'")) {
      var literal = token.Substring(1, token.Length - 2);
      return new ConstantValueExpr { Type = ScriptValue.ValueType.String, ValueFn = () => ScriptValue.Of(literal) };
    }
    if (token[0] >= '0' && token[0] <= '9') {
      if (!int.TryParse(token, out var number)) {
        throw new ScriptError($"Invalid number literal: {token}");
      }
      return new ConstantValueExpr { Type = ScriptValue.ValueType.Number, ValueFn = () => ScriptValue.Of(number) };
    }
    return null;
  }

  public string Serialize() {
    return Type switch {
        ScriptValue.ValueType.String => $"'{ValueFn().AsString}",
        ScriptValue.ValueType.Number => ValueFn().AsNumber.ToString(),
        _ => $"ERROR:{Type}",
    };
  }

  public override string ToString() {
    return $"{GetType().Name}#{Serialize()}";
  }
}
