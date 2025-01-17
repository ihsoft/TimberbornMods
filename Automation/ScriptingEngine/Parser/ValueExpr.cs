// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class ValueExpr : IExpression {
  public enum ValueType {
    Unknown,
    Number,
    String,
  }
  
  public ValueType Type = ValueType.Unknown;
  public Func<string> GetStringValue { get; init; } = () => throw new ScriptError("Not a string value");
  public Func<int> GetNumberValue { get; init; } = () => throw new ScriptError("Not a number value");

  public static ValueExpr CreateStringConstant(string literal) {
    return new ValueExpr {
        Type = ValueType.String,
        GetStringValue = () => literal.Substring(1, literal.Length - 2),
    };
  }

  public static ValueExpr CreateNumberConstant(string literal) {
    if (!int.TryParse(literal, out var number)) {
      throw new ScriptError($"Invalid number literal: {literal}");
    }
    return new ValueExpr {
        Type = ValueType.Number,
        GetNumberValue = () => number,
    };
  }
}
