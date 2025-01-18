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
    if (left.ValueType != right.ValueType) {
      throw new ScriptError($"Arguments type mismatch: {left.ValueType} != {right.ValueType}");
    }
    Execute = left.ValueType switch {
        ScriptValue.TypeEnum.String => name switch {
            "eq" => () => left.ValueFn().AsString == right.ValueFn().AsString,
            "ne" => () => left.ValueFn().AsString != right.ValueFn().AsString,
            _ => throw new ScriptError("Unsupported operator for string operands: " + name),
        },
        ScriptValue.TypeEnum.Number => name switch {
            "eq" => () => left.ValueFn().AsNumber == right.ValueFn().AsNumber,
            "ne" => () => left.ValueFn().AsNumber != right.ValueFn().AsNumber,
            "gt" => () => left.ValueFn().AsNumber > right.ValueFn().AsNumber,
            "lt" => () => left.ValueFn().AsNumber < right.ValueFn().AsNumber,
            "ge" => () => left.ValueFn().AsNumber >= right.ValueFn().AsNumber,
            "le" => () => left.ValueFn().AsNumber <= right.ValueFn().AsNumber,
            _ => throw new InvalidOperationException("Unknown operator: " + name),
        },
        _ => throw new InvalidOperationException($"Value type is unspecified: {left}, {right}"),
    };
  }
}
