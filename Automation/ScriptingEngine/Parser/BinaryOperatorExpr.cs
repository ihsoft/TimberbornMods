// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Text;

namespace IgorZ.Automation.ScriptingEngine.Parser;

sealed class BinaryOperatorExpr : BoolOperatorExpr {

  static readonly HashSet<string> Names = [ "eq", "ne", "gt", "lt", "ge", "le"];

  public SignalOperatorExpr Left => (SignalOperatorExpr)Operands[0];
  public IValueExpr Right => (IValueExpr)Operands[1];

  public static IExpression TryCreateFrom(string name, IList<IExpression> arguments) {
    return Names.Contains(name) ? new BinaryOperatorExpr(name, arguments) : null;
  }

  /// <inheritdoc/>
  public override string Describe() {
    var sb = new StringBuilder();
    sb.Append(Left.Describe());
    sb.Append(Name switch {
        "eq" => " = ",
        "ne" => " <> ",
        "gt" => " > ",
        "lt" => " < ",
        "ge" => " >= ",
        "le" => " <= ",
        _ => throw new InvalidOperationException("Unknown operator: " + Name),
    });
    if (Right is ConstantValueExpr constantValueExpr) {
      var def = ExpressionParser.Instance.GetTriggerDefinition(Left.SignalName);
      sb.Append(constantValueExpr.FormatValue(def.ResultType));
    } else {
      sb.Append(Right.Describe());
    }
    return sb.ToString();
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
