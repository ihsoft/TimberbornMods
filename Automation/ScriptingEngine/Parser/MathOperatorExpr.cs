// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class MathOperatorExpr : AbstractOperandExpr, IValueExpr {
  const string AddOperatorName = "add";
  const string SubOperatorName = "sub";
  const string MulOperatorName = "mul";
  const string DivOperatorName = "div";
  const string MinOperatorName = "min";
  const string MaxOperatorName = "max";
  const string RoundOperatorName = "round";

  /// <inheritdoc/>
  public ScriptValue.TypeEnum ValueType => ScriptValue.TypeEnum.Number;

  /// <inheritdoc/>
  public Func<ScriptValue> ValueFn { get; }

  /// <inheritdoc/>
  public override string Describe() {
    return Name switch {
        MinOperatorName or MaxOperatorName => $"{Name}({string.Join(", ", Operands.Select(x => x.Describe()))})",
        DivOperatorName => $"{Operands[0].Describe()} ÷ {Operands[1].Describe()}",
        MulOperatorName => $"{Operands[0].Describe()} × {Operands[1].Describe()}",
        AddOperatorName => $"({Operands[0].Describe()} + {Operands[1].Describe()})",
        SubOperatorName => $"({Operands[0].Describe()} - {Operands[1].Describe()})",
        RoundOperatorName => $"Round({Operands[0].Describe()})",
        _ => throw new InvalidDataException("Unknown operator: " + Name),
    };
  }

  public static IExpression TryCreateFrom(string name, IList<IExpression> arguments) {
    return name switch {
        AddOperatorName => new MathOperatorExpr(
            name, arguments, 2, 2, args => args[0].ValueFn() + args[1].ValueFn()),
        SubOperatorName => new MathOperatorExpr(
            name, arguments, 2, 2, args => args[0].ValueFn() - args[1].ValueFn()),
        MulOperatorName => new MathOperatorExpr(
            name, arguments, 2, 2, args => args[0].ValueFn() * args[1].ValueFn()),
        DivOperatorName => new MathOperatorExpr(
            name, arguments, 2, 2, args => args[0].ValueFn() / args[1].ValueFn()),
        RoundOperatorName => new MathOperatorExpr(
            name, arguments, 1, 1, args => ScriptValue.FromInt(args[0].ValueFn().AsInt)),
        MinOperatorName => new MathOperatorExpr(
            name, arguments, 2, -1,
            args => ScriptValue.Of(args.Min(x => x.ValueFn().AsNumber))),
        MaxOperatorName => new MathOperatorExpr(
            name, arguments, 2, -1,
            args => ScriptValue.Of(args.Max(x => x.ValueFn().AsNumber))),
        _ => null,
    };
  }

  MathOperatorExpr(string name, IList<IExpression> operands, int minArgs, int maxArgs,
                         Func<IList<IValueExpr>, ScriptValue> function) : base(name, operands) {
    AsserNumberOfOperandsRange(minArgs, maxArgs);
    var valueExprs = new List<IValueExpr>();
    for (var i = 0; i < operands.Count; i++) {
      var op = Operands[i];
      if (op is not IValueExpr { ValueType: ScriptValue.TypeEnum.Number } result) {
        throw new ScriptError($"Operand #{i + 1} must be a numeric value, found: {op}");
      }
      valueExprs.Add(result);
    }
    ValueFn = () => function(valueExprs);
  }
}
