// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.Settings;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

sealed class ComparisonOperator : BooleanOperator {

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

  public static ComparisonOperator CreateEq(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.Equal, context, operands);
  public static ComparisonOperator CreateNe(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.NotEqual, context, operands);
  public static ComparisonOperator CreateLt(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.LessThan, context, operands);
  public static ComparisonOperator CreateLe(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.LessThanOrEqual, context, operands);
  public static ComparisonOperator CreateGt(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.GreaterThan, context, operands);
  public static ComparisonOperator CreateGe(ExpressionContext context, IList<IExpression> operands) =>
      new(OpType.GreaterThanOrEqual, context, operands);

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType().Name}({OperatorType})";
  }

  ComparisonOperator(OpType opType, ExpressionContext context, IList<IExpression> operands) : base(operands) {
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
    IValueExpr otherArgExpr = null;
    if (left is SignalOperator leftSignal) {
      signalDef = ScriptingService.Instance.GetSignalDefinition(leftSignal.SignalName, context.ScriptHost);
    } else if (right is SignalOperator rightSignal) {
      signalDef = ScriptingService.Instance.GetSignalDefinition(rightSignal.SignalName, context.ScriptHost);
    }
    if (signalDef != null) {
      otherArgExpr = left is SignalOperator ? right : left;
      ResultValueDef = signalDef.Result;
      signalDef.Result.ArgumentValidator?.Invoke(otherArgExpr);
      if (otherArgExpr is ConstantValueExpr constantValueExpr) {
        if (constantValueExpr.ValidateAndMaybeCorrect(ResultValueDef, out var newValueExpr)) {
          DebugEx.Warning("ComparisonOperator: Replacing constant value '{0}' with '{1}' for signal {2}",
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
    Func<bool> executeFn = left.ValueType switch {
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
            _ => throw new InvalidOperationException($"Unknown comparison operator: {opType}"),
        },
        _ => throw new InvalidOperationException($"Unexpected operand type: {left.ValueType}"),
    };
    if (signalDef?.Result.RuntimeValueValidator == null || otherArgExpr.IsConstantValue()) {
      // If there is a signal, then we have value definition and can validate the constant argument.
      signalDef?.Result.RuntimeValueValidator?.Invoke(otherArgExpr.ValueFn());
      Execute = executeFn;
    } else {
      Execute = () => {
        if (ScriptEngineSettings.CheckArgumentValues) {
          signalDef.Result.RuntimeValueValidator(otherArgExpr.ValueFn());
        }
        return executeFn();
      };
    }
  }
}
