// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class MathOperator : AbstractOperator, IValueExpr {
  const string AddOperatorName = "add";
  const string SubOperatorName = "sub";
  const string MulOperatorName = "mul";
  const string DivOperatorName = "div";
  const string ModOperatorName = "mod";
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
        AddOperatorName => $"({Operands.Select(x => x.Describe()).Aggregate((a, b) => a + " + " + b)})",
        SubOperatorName => $"({Operands[0].Describe()} - {Operands[1].Describe()})",
        MulOperatorName => $"{Operands[0].Describe()} × {Operands[1].Describe()}",
        DivOperatorName => $"{Operands[0].Describe()} ÷ {Operands[1].Describe()}",
        ModOperatorName => $"{Operands[0].Describe()} % {Operands[1].Describe()}",
        MinOperatorName or MaxOperatorName => $"{Name}({string.Join(", ", Operands.Select(x => x.Describe()))})",
        RoundOperatorName => $"Round({Operands[0].Describe()})",
        _ => throw new InvalidDataException("Unknown operator: " + Name),
    };
  }

  public static IExpression TryCreateFrom(string name, IList<IExpression> arguments) {
    return name switch {
        AddOperatorName => new MathOperator(
            name, arguments, 2, -1, args => args.Select(x => x.ValueFn()).Aggregate((a, b) => a + b)),
        SubOperatorName => new MathOperator(
            name, arguments, 2, 2, args => args[0].ValueFn() - args[1].ValueFn()),
        MulOperatorName => new MathOperator(
            name, arguments, 2, 2, args => args[0].ValueFn() * args[1].ValueFn()),
        DivOperatorName => new MathOperator(
            name, arguments, 2, 2, args => args[0].ValueFn() / args[1].ValueFn()),
        ModOperatorName => new MathOperator(
            name, arguments, 2, 2, args => ScriptValue.FromFloat(args[0].ValueFn().AsFloat % args[1].ValueFn().AsFloat)),
        MinOperatorName => new MathOperator(
            name, arguments, 2, -1, args => args.Select(x => x.ValueFn()).Min()),
        MaxOperatorName => new MathOperator(
            name, arguments, 2, -1, args => args.Select(x => x.ValueFn()).Max()),
        RoundOperatorName => new MathOperator(
            name, arguments, 1, 1, args => ScriptValue.FromInt(args[0].ValueFn().AsInt)),
        _ => null,
    };
  }

  MathOperator(string name, IList<IExpression> operands, int minArgs, int maxArgs,
                         Func<IList<IValueExpr>, ScriptValue> function) : base(name, operands) {
    AsserNumberOfOperandsRange(minArgs, maxArgs);
    var valueExprs = new List<IValueExpr>();
    for (var i = 0; i < operands.Count; i++) {
      var op = Operands[i];
      if (op is not IValueExpr { ValueType: ScriptValue.TypeEnum.Number } result) {
        throw new ScriptError.ParsingError($"Operand #{i + 1} must be a numeric value, found: {op}");
      }
      valueExprs.Add(result);
    }
    ValueFn = () => function(valueExprs);
  }
}
