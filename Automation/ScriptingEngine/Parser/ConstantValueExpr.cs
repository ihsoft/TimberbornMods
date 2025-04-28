// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Parser;

sealed class ConstantValueExpr : IValueExpr {

  public ScriptValue.TypeEnum ValueType { get; private init; }
  public Func<ScriptValue> ValueFn { get; private init; }

  public static IExpression TryCreateFrom(string token) {
    if (token.StartsWith("'")) {
      var literal = token.Substring(1, token.Length - 2);
      return new ConstantValueExpr { ValueType = ScriptValue.TypeEnum.String, ValueFn = () => ScriptValue.Of(literal) };
    }
    if (token[0] >= '0' && token[0] <= '9' || token[0] == '-') {
      if (!int.TryParse(token, out var number)) {
        throw new ScriptError.ParsingError($"Invalid number literal: {token}");
      }
      return new ConstantValueExpr { ValueType = ScriptValue.TypeEnum.Number, ValueFn = () => ScriptValue.Of(number) };
    }
    return null;
  }

  /// <inheritdoc/>
  public string Serialize() {
    return ValueType switch {
        ScriptValue.TypeEnum.String => $"'{ValueFn().AsString}'",
        ScriptValue.TypeEnum.Number => ValueFn().AsNumber.ToString(),
        _ => $"ERROR:{ValueType}",
    };
  }

  /// <inheritdoc/>
  public string Describe() {
    return Serialize();
  }

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType().Name}#{Serialize()}";
  }
}
