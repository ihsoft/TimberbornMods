// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Core;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

class MathOperator : AbstractOperator, IValueExpr {
  public enum OpType {
      Add, Subtract, Multiply, Divide, Modulus, Min, Max, Round, Negate,
  }

  public readonly OpType OperatorType;

  /// <inheritdoc/>
  public ScriptValue.TypeEnum ValueType => ScriptValue.TypeEnum.Number;

  /// <inheritdoc/>
  public Func<ScriptValue> ValueFn { get; }

  public static MathOperator CreateAdd(IList<IExpression> arguments) => new(OpType.Add, arguments, 2, -1);
  public static MathOperator CreateSubtract(IList<IExpression> arguments) => new(OpType.Subtract, arguments, 2, 2);
  public static MathOperator CreateMultiply(IList<IExpression> arguments) => new(OpType.Multiply, arguments, 2, 2);
  public static MathOperator CreateDivide(IList<IExpression> arguments) => new(OpType.Divide, arguments, 2, 2);
  public static MathOperator CreateModulus(IList<IExpression> arguments) => new(OpType.Modulus, arguments, 2, 2);
  public static MathOperator CreateMin(IList<IExpression> arguments) => new(OpType.Min, arguments, 2, -1);
  public static MathOperator CreateMax(IList<IExpression> arguments) => new(OpType.Max, arguments, 2, -1);
  public static MathOperator CreateRound(IList<IExpression> arguments) => new(OpType.Round, arguments, 1, 1);
  public static MathOperator CreateNegate(IExpression argument) => new(OpType.Negate, [argument], 1, 1);

  /// <inheritdoc/>
  public override IList<IExpression> GetReducedOperands() {
    if (OperatorType is OpType.Min or OpType.Max) {
      throw new InvalidOperationException($"Min/Max are functions, not operands reducing expected");
    }
    // All, but "add" operator are protected to not have more than 2 operands.
    return ReducedOperands(CreateAdd);
  }

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType().Name}({OperatorType})";
  }

  MathOperator(OpType opType, IList<IExpression> operands, int minArgs, int maxArgs) : base(operands) {
    OperatorType = opType;
    AssertNumberOfOperandsRange(minArgs, maxArgs);
    var args = new List<IValueExpr>();
    for (var i = 0; i < operands.Count; i++) {
      var op = Operands[i];
      if (op is not IValueExpr { ValueType: ScriptValue.TypeEnum.Number } result) {
        throw new ScriptError.ParsingError($"Operand #{i + 1} must be a numeric value, found: {op}");
      }
      args.Add(result);
    }
    ValueFn = opType switch {
        OpType.Add => () => args.Select(x => x.ValueFn()).Aggregate((a, b) => a + b),
        OpType.Subtract => () => args[0].ValueFn() - args[1].ValueFn(),
        OpType.Multiply => () => args[0].ValueFn() * args[1].ValueFn(),
        OpType.Divide => () => args[0].ValueFn() / args[1].ValueFn(),
        OpType.Modulus => () => ScriptValue.FromFloat(args[0].ValueFn().AsFloat % args[1].ValueFn().AsFloat),
        OpType.Min => () => args.Select(x => x.ValueFn()).Min(),
        OpType.Max => () => args.Select(x => x.ValueFn()).Max(),
        OpType.Round => () => ScriptValue.FromInt(args[0].ValueFn().AsInt),
        OpType.Negate => () => -args[0].ValueFn(),
        _ => throw new InvalidOperationException($"Unknown math operator: {opType}"),
    };
  }
}
