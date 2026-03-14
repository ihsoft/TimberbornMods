using IgorZ.Automation.ScriptingEngine.Expressions;

namespace IgorZ.Automation.ScriptingEngine.Parser;

static class InfixExpressionUtil {
  public static int ResolvePrecedence(IExpression expression) {
    return expression switch {
        LogicalOperator { OperatorType: LogicalOperator.OpType.Or} => 0,
        LogicalOperator { OperatorType: LogicalOperator.OpType.And} => 1,
        LogicalOperator { OperatorType: LogicalOperator.OpType.Not} => 2,
        ComparisonOperator { OperatorType: ComparisonOperator.OpType.Equal} => 3,
        ComparisonOperator { OperatorType: ComparisonOperator.OpType.NotEqual} => 3,
        ComparisonOperator { OperatorType: ComparisonOperator.OpType.LessThan} => 4,
        ComparisonOperator { OperatorType: ComparisonOperator.OpType.LessThanOrEqual} => 4,
        ComparisonOperator { OperatorType: ComparisonOperator.OpType.GreaterThan} => 4,
        ComparisonOperator { OperatorType: ComparisonOperator.OpType.GreaterThanOrEqual} => 4,
        MathOperator { OperatorType: MathOperator.OpType.Add} => 5,
        MathOperator { OperatorType: MathOperator.OpType.Subtract} => 5,
        MathOperator { OperatorType: MathOperator.OpType.Modulus} => 6,
        MathOperator { OperatorType: MathOperator.OpType.Divide} => 6,
        MathOperator { OperatorType: MathOperator.OpType.Multiply} => 6,
        // Values, variables and functions.
        _ => 100,
    };
  }
}
