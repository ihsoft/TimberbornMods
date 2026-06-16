using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;

namespace Automation.Tests;

static class ExpressionTests {
  public static void ScriptValueStoresFixedPrecisionNumbers() {
    var integer = ScriptValue.FromInt(12);
    var rounded = ScriptValue.FromFloat(1.236f);
    var boolean = ScriptValue.FromBool(true);
    var text = ScriptValue.FromString("value");

    Assert.Equal(ScriptValue.TypeEnum.Number, integer.ValueType);
    Assert.Equal(1200, integer.AsRawNumber);
    Assert.Equal(12, integer.AsInt);
    Assert.Equal(124, rounded.AsRawNumber);
    Assert.Equal(100, boolean.AsRawNumber);
    Assert.Equal(ScriptValue.TypeEnum.String, text.ValueType);
    Assert.Equal("value", text.AsString);
  }

  public static void ScriptValueArithmeticUsesRawFixedPrecision() {
    Assert.Equal(350, (ScriptValue.FromFloat(1.5f) + ScriptValue.FromInt(2)).AsRawNumber);
    Assert.Equal(-50, (ScriptValue.FromFloat(1.5f) - ScriptValue.FromInt(2)).AsRawNumber);
    Assert.Equal(300, (ScriptValue.FromFloat(1.5f) * ScriptValue.FromInt(2)).AsRawNumber);
    Assert.Equal(75, (ScriptValue.FromFloat(1.5f) / ScriptValue.FromInt(2)).AsRawNumber);
    Assert.Equal(-150, (-ScriptValue.FromFloat(1.5f)).AsRawNumber);
    Assert.Throws<ScriptError.DivisionByZero>(() => _ = ScriptValue.FromInt(1) / ScriptValue.Of(0));
  }

  public static void ScriptValueRejectsInvalidAccess() {
    var number = ScriptValue.FromInt(1);
    var text = ScriptValue.FromString("value");
    var unset = ScriptValue.InvalidValue;

    Assert.Throws<ScriptError.BadValue>(() => _ = number.AsString);
    Assert.Throws<ScriptError.BadValue>(() => _ = text.AsRawNumber);
    Assert.Throws<InvalidOperationException>(() => _ = number.CompareTo(text));
    Assert.Throws<InvalidOperationException>(() => _ = unset.CompareTo(ScriptValue.InvalidValue));
    Assert.Equal("ScriptValue#Number:100", number.ToString());
    Assert.Equal("ScriptValue#String:value", text.ToString());
    Assert.Equal("ScriptValue#Unset", unset.ToString());
  }

  public static void MathOperatorExecutesNumericFunctions() {
    Assert.Equal(600, MathOperator.CreateAdd([Number(1), Number(2), Number(3)]).ValueFn().AsRawNumber);
    Assert.Equal(200, MathOperator.CreateSubtract([Number(5), Number(3)]).ValueFn().AsRawNumber);
    Assert.Equal(750, MathOperator.CreateMultiply([Number(2.5f), Number(3)]).ValueFn().AsRawNumber);
    Assert.Equal(250, MathOperator.CreateDivide([Number(5), Number(2)]).ValueFn().AsRawNumber);
    Assert.Equal(100, MathOperator.CreateModulus([Number(21), Number(5)]).ValueFn().AsRawNumber);
    Assert.Equal(100, MathOperator.CreateMin([Number(3), Number(1), Number(2)]).ValueFn().AsRawNumber);
    Assert.Equal(300, MathOperator.CreateMax([Number(3), Number(1), Number(2)]).ValueFn().AsRawNumber);
    Assert.Equal(200, MathOperator.CreateRound([Number(1.61f)]).ValueFn().AsRawNumber);
    Assert.Equal(-200, MathOperator.CreateNegate(Number(2)).ValueFn().AsRawNumber);
  }

  public static void ComparisonOperatorExecutesComparisons() {
    Assert.True(ComparisonOperator.CreateEq(Context(), [Number(2), Number(2)]).Execute());
    Assert.True(ComparisonOperator.CreateNe(Context(), [Text("one"), Text("two")]).Execute());
    Assert.True(ComparisonOperator.CreateLt(Context(), [Number(1), Number(2)]).Execute());
    Assert.True(ComparisonOperator.CreateLe(Context(), [Number(2), Number(2)]).Execute());
    Assert.True(ComparisonOperator.CreateGt(Context(), [Number(3), Number(2)]).Execute());
    Assert.True(ComparisonOperator.CreateGe(Context(), [Number(3), Number(3)]).Execute());
    Assert.False(ComparisonOperator.CreateEq(Context(), [Text("one"), Text("two")]).Execute());
    Assert.Throws<ScriptError.ParsingError>(() => ComparisonOperator.CreateGt(Context(), [Text("one"), Text("two")]));
  }

  public static void LogicalOperatorExecutesBooleanComposition() {
    var truth = ComparisonOperator.CreateEq(Context(), [Number(1), Number(1)]);
    var lie = ComparisonOperator.CreateEq(Context(), [Number(1), Number(2)]);

    Assert.True(LogicalOperator.CreateAnd([truth, truth]).Execute());
    Assert.False(LogicalOperator.CreateAnd([truth, lie]).Execute());
    Assert.True(LogicalOperator.CreateOr([lie, truth]).Execute());
    Assert.False(LogicalOperator.CreateOr([lie, lie]).Execute());
    Assert.True(LogicalOperator.CreateNot(lie).Execute());
  }

  public static void ConcatOperatorConcatenatesValues() {
    var concat = ConcatOperator.Create([Text("value="), Number(1.5f), Text("; sum="), Sum(Number(1), Number(2))]);

    Assert.Equal(ScriptValue.TypeEnum.String, concat.ValueType);
    Assert.Equal("value=1.5; sum=3", concat.ValueFn().AsString);
  }

  public static void OperatorsRejectInvalidOperands() {
    Assert.Throws<ScriptError.ParsingError>(() => MathOperator.CreateAdd([Number(1)]));
    Assert.Throws<ScriptError.ParsingError>(() => MathOperator.CreateAdd([Number(1), Text("bad")]));
    Assert.Throws<ScriptError.ParsingError>(() => ComparisonOperator.CreateEq(Context(), [Number(1), Text("1")]));
    Assert.Throws<ScriptError.ParsingError>(() => LogicalOperator.CreateAnd([Bool(true), Number(1)]));
    Assert.Throws<ScriptError.ParsingError>(() => ConcatOperator.Create([Text("one")]));
  }

  static ExpressionContext Context() {
    return new ExpressionContext { ScriptHost = new AutomationBehavior() };
  }

  static ConstantValueExpr Number(int value) {
    return ConstantValueExpr.CreateFromValue(ScriptValue.FromInt(value));
  }

  static ConstantValueExpr Number(float value) {
    return ConstantValueExpr.CreateFromValue(ScriptValue.FromFloat(value));
  }

  static ConstantValueExpr Text(string value) {
    return ConstantValueExpr.CreateStringLiteral(value);
  }

  static MathOperator Sum(IExpression left, IExpression right) {
    return MathOperator.CreateAdd([left, right]);
  }

  static BooleanOperator Bool(bool value) {
    return ComparisonOperator.CreateEq(Context(), [Number(value ? 1 : 0), Number(1)]);
  }
}
