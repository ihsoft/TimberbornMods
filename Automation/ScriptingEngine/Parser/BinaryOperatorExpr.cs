// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class BinaryOperatorExpr : BoolOperatorExpr {
  static readonly HashSet<string> Names = [ "eq", "ne", "gt", "lt", "ge", "le"]; 

  public static IExpression TryCreateFrom(string name, IList<IExpression> arguments) {
    return Names.Contains(name) ? new BinaryOperatorExpr(name, arguments) : null;
  }

  BinaryOperatorExpr(string name, IList<IExpression> operands) : base(name, operands) {
    AsserNumberOfOperandsExact(2);
    if (Operands[0] is not SignalOperatorExpr left) {
      throw new ScriptError("Left operand must be a signal: " + Operands[0]);
    }
    if (Operands[1] is not IValueExpr right) {
      throw new ScriptError("Right operand must be a value: " + Operands[1]);
    }
    if (left.Type != right.Type) {
      throw new ScriptError($"Arguments type mismatch: {left.Type} != {right.Type}");
    }
    Execute = left.Type switch {
        IValueExpr.ValueType.String => name switch {
            "eq" => () => left.GetStringValue() == right.GetStringValue(),
            "ne" => () => left.GetStringValue() != right.GetStringValue(),
            _ => throw new ScriptError("Unsupported operator for string operands: " + name),
        },
        IValueExpr.ValueType.Number => name switch {
            "eq" => () => left.GetNumberValue() == right.GetNumberValue(),
            "ne" => () => left.GetNumberValue() != right.GetNumberValue(),
            "gt" => () => left.GetNumberValue() > right.GetNumberValue(),
            "lt" => () => left.GetNumberValue() < right.GetNumberValue(),
            "ge" => () => left.GetNumberValue() >= right.GetNumberValue(),
            "le" => () => left.GetNumberValue() <= right.GetNumberValue(),
            _ => throw new InvalidOperationException("Unknown operator: " + name),
        },
        _ => throw new InvalidOperationException($"Value type is unspecified: {left}, {right}"),
    };
  }
}
