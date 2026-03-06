// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

//FIXME: ComparisonOperator
sealed class BinaryOperator : BoolOperator {

  public enum OpType {
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
  }

  public readonly OpType OperatorType;
  public IValueExpr Left => (IValueExpr)Operands[0];
  public IValueExpr Right => (IValueExpr)Operands[1];
  public readonly ValueDef ResultValueDef;

  public static BinaryOperator CreateEq(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.Equal, context, operands);
  public static BinaryOperator CreateNe(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.NotEqual, context, operands);
  public static BinaryOperator CreateLt(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.LessThan, context, operands);
  public static BinaryOperator CreateLe(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.LessThanOrEqual, context, operands);
  public static BinaryOperator CreateGt(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.GreaterThan, context, operands);
  public static BinaryOperator CreateGe(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.GreaterThanOrEqual, context, operands);

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType().Name}({OperatorType})";
  }

  BinaryOperator(OpType opType, ExpressionContext context, IList<IExpression> operands) : base(operands) {
    OperatorType = opType;
    AssertNumberOfOperandsExact(2);
    if (Operands[0] is not IValueExpr left) {
      throw new ScriptError.ParsingError("Left operand must be a value, found: " + Operands[0]);
    }
    if (Operands[1] is not IValueExpr right) {
      throw new ScriptError.ParsingError("Right operand must be a value, found: " + Operands[1]);
    }
    if (left.ValueType != right.ValueType) {
      throw new ScriptError.ParsingError($"Arguments type mismatch: {left.ValueType} != {right.ValueType}");
    }
    SignalDef signalDef = null;
    if (left is SignalOperator leftSignal) {
      signalDef = ScriptingService.Instance.GetSignalDefinition(leftSignal.SignalName, context.ScriptHost);
    } else if (right is SignalOperator rightSignal) {
      signalDef = ScriptingService.Instance.GetSignalDefinition(rightSignal.SignalName, context.ScriptHost);
    }
    if (signalDef != null) {
      var otherArgExpr = left is SignalOperator ? right : left;
      ResultValueDef = signalDef.Result;
      signalDef.Result.ArgumentValidator?.Invoke(otherArgExpr);
      if (otherArgExpr is ConstantValueExpr constantValueExpr) {
        if (constantValueExpr.ValidateAndMaybeCorrect(ResultValueDef, out var newValueExpr)) {
          DebugEx.Warning("BinaryOperator: Replacing constant value '{0}' with '{1}' for signal {2}",
                          constantValueExpr, newValueExpr, signalDef.ScriptName);
          if (Operands[0] == otherArgExpr) {
            Operands[0] = newValueExpr;
            left = newValueExpr;
          } else {
            Operands[1] = newValueExpr;
            right = newValueExpr;
          }
        }
      }
    }
    Execute = left.ValueType switch {
        ScriptValue.TypeEnum.String => opType switch {
            OpType.Equal => () => left.ValueFn().AsString == right.ValueFn().AsString,
            OpType.NotEqual => () => left.ValueFn().AsString != right.ValueFn().AsString,
            _ => throw new ScriptError.ParsingError("Unsupported operator for string operands: " + opType),
        },
        ScriptValue.TypeEnum.Number => opType switch {
            OpType.Equal => () => left.ValueFn().AsNumber == right.ValueFn().AsNumber,
            OpType.NotEqual => () => left.ValueFn().AsNumber != right.ValueFn().AsNumber,
            OpType.LessThan => () => left.ValueFn().AsNumber < right.ValueFn().AsNumber,
            OpType.LessThanOrEqual => () => left.ValueFn().AsNumber <= right.ValueFn().AsNumber,
            OpType.GreaterThan => () => left.ValueFn().AsNumber > right.ValueFn().AsNumber,
            OpType.GreaterThanOrEqual => () => left.ValueFn().AsNumber >= right.ValueFn().AsNumber,
            _ => throw new ArgumentOutOfRangeException(nameof(opType), opType, null),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(ValueType), left.ValueType, null),
    };
  }
}
