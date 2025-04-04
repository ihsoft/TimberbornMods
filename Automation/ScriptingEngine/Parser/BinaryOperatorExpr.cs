// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IgorZ.Automation.ScriptingEngine.Parser;

sealed class BinaryOperatorExpr : BoolOperatorExpr {

  static readonly HashSet<string> Names = [ "eq", "ne", "gt", "lt", "ge", "le"];

  public SignalOperatorExpr Left => (SignalOperatorExpr)Operands[0];
  public IValueExpr Right => (IValueExpr)Operands[1];

  readonly SignalDef _signalDef;

  public static IExpression TryCreateFrom(ExpressionParser.Context context, string name, IList<IExpression> arguments) {
    return Names.Contains(name) ? new BinaryOperatorExpr(context, name, arguments) : null;
  }

  /// <inheritdoc/>
  public override string Describe() {
    var sb = new StringBuilder();
    sb.Append(Left.Describe());
    sb.Append(Name switch {
        "eq" => " = ",
        "ne" => " \u2260 ",
        "gt" => " > ",
        "lt" => " < ",
        "ge" => " \u2265 ",
        "le" => " \u2264 ",
        _ => throw new InvalidOperationException("Unknown operator: " + Name),
    });
    sb.Append(Right.ValueFn().FormatValue(_signalDef.Result));
    return sb.ToString();
  }

  BinaryOperatorExpr(ExpressionParser.Context context, string name, IList<IExpression> operands)
      : base(name, operands) {
    AsserNumberOfOperandsExact(2);
    if (Operands[0] is not SignalOperatorExpr left) {
      throw new ScriptError("Left operand must be a signal, found: " + Operands[0]);
    }
    if (Operands[1] is not IValueExpr right) {
      throw new ScriptError("Right operand must be a value, found: " + Operands[1]);
    }
    if (left.ValueType != right.ValueType) {
      throw new ScriptError($"Arguments type mismatch: {left.ValueType} != {right.ValueType}");
    }
    _signalDef = context.ScriptingService.GetSignalDefinition(left.SignalName, context.ScriptHost);
    if (right is ConstantValueExpr { ValueType: ScriptValue.TypeEnum.String } constantValueExpr) {
      var option = constantValueExpr.ValueFn().AsString;
      if (_signalDef.Result.Options != null && _signalDef.Result.Options.All(x => x.Value != option)) {
        throw new ScriptError($"Unexpected value: {option}");
      }
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
