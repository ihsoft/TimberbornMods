// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.UI;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class BinaryOperatorExpr : BoolOperatorExpr {
  static readonly HashSet<string> Names = [ "eq", "ne", "gt", "lt", "ge", "le"]; 

  public static IExpression TryCreateFrom(string name, IList<IExpression> arguments) {
    return Names.Contains(name) ? new BinaryOperatorExpr(name, arguments) : null;
  }

  BinaryOperatorExpr(string name, IList<IExpression> operands) : base(name, operands, 2) {
    var left = GetOperand<SignalOperatorExpr>(Operands[0]);
    var right = GetOperand<ValueExpr>(Operands[1]);
    if (left.Type != right.Type) {
      throw new ScriptError("Arguments type mismatch: " + left.Type + " != " + right.Type);
    }
    if (left.Type == ValueExpr.ValueType.String) {
      Execute = name switch {
          "eq" => () => left.GetStringValue() == right.GetStringValue(),
          "ne" => () => left.GetStringValue() != right.GetStringValue(),
          _ => throw new ScriptError("Unsupported operator for string operands: " + name),
      };
    }
    if (left.Type == ValueExpr.ValueType.Number) {
      Execute = name switch {
          "eq" => () => left.GetNumberValue() == right.GetNumberValue(),
          "ne" => () => left.GetNumberValue() != right.GetNumberValue(),
          "gt" => () => left.GetNumberValue() > right.GetNumberValue(),
          "lt" => () => left.GetNumberValue() < right.GetNumberValue(),
          "ge" => () => left.GetNumberValue() >= right.GetNumberValue(),
          "le" => () => left.GetNumberValue() <= right.GetNumberValue(),
          _ => throw new InvalidOperationException("Unknown operator: " + name),
      };
    } else {
      throw new InvalidOperationException($"Value type is unspecified: {left}, {right}");
    }
  }
}
