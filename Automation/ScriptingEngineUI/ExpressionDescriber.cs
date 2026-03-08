// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using System.Text;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.Localization;

namespace IgorZ.Automation.ScriptingEngineUI;

/// <summary>Makes a human-readable description of the parsed expression.</summary>
sealed class ExpressionDescriber(ILoc Loc) {

  const string AndOperatorLocKey = "IgorZ.Automation.Scripting.Expressions.AndOperator";
  const string OrOperatorLocKey = "IgorZ.Automation.Scripting.Expressions.OrOperator";
  const string NotOperatorLocKey = "IgorZ.Automation.Scripting.Expressions.NotOperator";

  /// <summary>Returns a human-friendly description of the expression.</summary>
  /// <exception cref="ScriptError.RuntimeError">if values need to be calculated, but it results in error.</exception>
  public string DescribeExpression(IExpression expression) {
    return DescribeExpressionInternal(expression);
  }

  /// <summary>Formats the value according to the value definition for the display purpose.</summary>
  /// <param name="scriptValue">The value to format.</param>
  /// <param name="valueDef">
  /// Optional value definition. If not provided, then the string types are presented "as-is", and the number types are
  /// converted to floats and formatted as "0.##".
  /// </param>
  public static string FormatValue(ScriptValue scriptValue, ValueDef valueDef) {
    var stringValue = scriptValue.ValueType switch {
        ScriptValue.TypeEnum.Number => valueDef?.DisplayNumericFormat switch {
            ValueDef.NumericFormatEnum.Float => scriptValue.AsFloat.ToString("0.00"),
            ValueDef.NumericFormatEnum.Percent => scriptValue.AsFloat.ToString("0%"),
            ValueDef.NumericFormatEnum.Integer => scriptValue.AsInt.ToString(),
            null => scriptValue.AsFloat.ToString("0.##"),  // valeDef can be null.
            _ => throw new InvalidOperationException($"Unknown numeric format: {valueDef.DisplayNumericFormat}"),
        },
        ScriptValue.TypeEnum.String => scriptValue.AsString,
        ScriptValue.TypeEnum.Unset => throw new InvalidOperationException($"Cannot format value: {scriptValue}"),
        _ => throw new InvalidOperationException($"Unknown ScriptValue type: {scriptValue.ValueType}"),
    };
    if (valueDef?.Options == null) {
      return stringValue;
    }
    var resolvedValue = valueDef.Options.FirstOrDefault(x => x.Value == stringValue);
    return resolvedValue.Text ?? CommonFormats.HighlightRed("?" + stringValue);
  }

  #region Implementation

  string DescribeExpressionInternal(IExpression expression) {
    return expression switch {
        AbstractFunction abstractFunction => DescribeFunction(abstractFunction),
        ActionOperator actionOperator => DescribeActionOperator(actionOperator),
        ComparisonOperator comparisonOperator => DescribeComparisonOperator(comparisonOperator),
        ConcatOperator concatOperator => concatOperator.ValueFn().AsString,
        ConstantValueExpr constantValueExpr => DescribeScriptValue(constantValueExpr.ValueFn()),
        LogicalOperator logicalOperator => DescribeLogicalOperator(logicalOperator),
        MathOperator mathOperator => DescribeMathOperator(mathOperator),
        SignalOperator signalOperator => signalOperator.SignalDef.DisplayName,
        _ => throw new ScriptError.ParsingError($"Unexpected expression: {expression}"),
    };
  }

  string DescribeScriptValue(ScriptValue scriptValue) {
    return scriptValue.ValueType switch {
        ScriptValue.TypeEnum.String => $"'{scriptValue.AsString}'",
        ScriptValue.TypeEnum.Number => scriptValue.AsFloat.ToString("0.0#"),
        _ => $"ERROR:{scriptValue.ValueType}",
    };
  }

  string DescribeComparisonOperator(ComparisonOperator op) {
    // Special case: check for if "signal changed" binding (signal equals to itself).
    if (op.Left is SignalOperator leftSignal && op.Right is SignalOperator rightSignal
        && leftSignal.SignalName == rightSignal.SignalName && op.OperatorType == ComparisonOperator.OpType.Equal) {
      return DescribeExpressionInternal(leftSignal);
    }
    var sb = new StringBuilder();
    sb.Append(DescribeLeft(op.Left, op));
    sb.Append(op.OperatorType switch {
        ComparisonOperator.OpType.Equal => " = ",
        ComparisonOperator.OpType.NotEqual => " \u2260 ",
        ComparisonOperator.OpType.GreaterThan => " > ",
        ComparisonOperator.OpType.LessThan => " < ",
        ComparisonOperator.OpType.GreaterThanOrEqual => " \u2265 ",
        ComparisonOperator.OpType.LessThanOrEqual => " \u2264 ",
        _ => throw new InvalidOperationException("Unknown operator: " + this),
    });
    if (EntityPanelSettings.EvalValuesInConditions || op.Right.IsConstantValue()) {
      string rightValue;
      try {
        rightValue = FormatValue(op.Right.ValueFn(), op.ResultValueDef);
      } catch (ScriptError.BadValue e) {
        rightValue = Loc.T(e.LocKey);
      }
      sb.Append(rightValue);
    } else {
      sb.Append(DescribeRight(op.Right, op));
    }

    return sb.ToString();
  }

  string DescribeFunction(AbstractFunction function) {
    if (function is GetPropertyFunction getPropertyFunction) {
      var propertyName = getPropertyFunction.PropertyFullName;
      return getPropertyFunction.FunctionName switch {
          GetPropertyFunction.FuncName.Value => $"ValueOf({propertyName})",
          GetPropertyFunction.FuncName.Element =>
              $"GetElement({propertyName}, {DescribeExpressionInternal(getPropertyFunction.IndexExpr)})",
          GetPropertyFunction.FuncName.Length => $"Count({propertyName})",
          _ => throw new InvalidOperationException(
              $"Unexpected GetPropertyFunction type: {getPropertyFunction.FunctionName}")
      };
    }
    throw new InvalidOperationException($"Unexpected function: {function}");
  }

  string DescribeLogicalOperator(LogicalOperator op) {
    if (op.OperatorType == LogicalOperator.OpType.Not) {
      var value = DescribeLeft(op.Operands[0], op);
      return $"{Loc.T(NotOperatorLocKey)} {value}";
    }
    var displayName = op.OperatorType switch {
        LogicalOperator.OpType.And => Loc.T(AndOperatorLocKey),
        LogicalOperator.OpType.Or => Loc.T(OrOperatorLocKey),
        _ => throw new InvalidOperationException($"Unsupported operator: {op.OperatorType}"),
    };

    // Resolve the multi-operands operators: (add a b c ...)
    var operands = op.GetReducedOperands();
    var leftValue = DescribeLeft(operands[0], op);
    var rightValue = DescribeRight(operands[1], op);
    return $"{leftValue} {displayName} {rightValue}";
  }

  string DescribeMathOperator(MathOperator op) {
    if ((op as IValueExpr).IsConstantValue()) {
      return DescribeScriptValue(op.ValueFn());
    }

    // Functions don't need precedence check.
    var funcName = op.OperatorType switch {
        MathOperator.OpType.Min => "Min",
        MathOperator.OpType.Max => "Max",
        MathOperator.OpType.Round => "Round",
        // For constants, the negate operator should have been resolved above.
        MathOperator.OpType.Negate => "-",
        _ => null,
    };
    if (funcName != null) {
      var value = string.Join(", ", op.Operands.Select(DescribeExpressionInternal));
      return $"{funcName}({value})";
    }

    var operands = op.GetReducedOperands();
    var opName = op.OperatorType switch {
        MathOperator.OpType.Add => " + ",
        MathOperator.OpType.Subtract => " - ",
        MathOperator.OpType.Multiply => " × ",
        MathOperator.OpType.Divide => " ÷ ",
        MathOperator.OpType.Modulus => " % ",
        _ => throw new InvalidOperationException($"Unknown operator: {op.OperatorType}"),
    };
    var leftValue = DescribeLeft(operands[0], op);
    // Add and Multiply operators are not strictly left-associative. Avoid unneeded parenthesis.
    var rightValue = op.OperatorType is MathOperator.OpType.Add or MathOperator.OpType.Divide
        ? DescribeLeft(operands[1], op)
        : DescribeRight(operands[1], op);
    return $"{leftValue} {opName} {rightValue}";
  }

  string DescribeActionOperator(ActionOperator op) {
    var args = new object[op.ActionDef.Arguments.Length];
    for (var i = 0; i < op.ActionDef.Arguments.Length; i++) {
      var operand = op.Operands[i] as IValueExpr;
      if (EntityPanelSettings.EvalValuesInActionArguments || operand!.IsConstantValue()) {
        ScriptValue value;
        try {
          value = operand!.ValueFn();
        } catch (ScriptError.BadValue e) {
          return Loc.T(e.LocKey);
        }
        args[i] = FormatValue(value, op.ActionDef.Arguments[i]);
      } else {
        args[i] = DescribeExpressionInternal(operand);
      }
    }
    return string.Format(op.ActionDef.DisplayName, args);
  }

  string DescribeLeft(IExpression operand, IExpression parent) {
    var value = DescribeExpressionInternal(operand);
    return InfixExpressionUtil.ResolvePrecedence(parent) > InfixExpressionUtil.ResolvePrecedence(operand)
        ? $"({value})"
        : value;
  }

  string DescribeRight(IExpression operand, IExpression parent) {
    var value = DescribeExpressionInternal(operand);
    return InfixExpressionUtil.ResolvePrecedence(parent) >= InfixExpressionUtil.ResolvePrecedence(operand)
        ? $"({value})"
        : value;
  }

  #endregion
}
