// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IgorZ.Automation.Settings;
using TimberApi.DependencyContainerSystem;
using Timberborn.Localization;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.Parser;

sealed class BinaryOperator : BoolOperator {

  const string SignalChangedLocKey = "IgorZ.Automation.Scripting.Expressions.SignalChanged";

  static readonly HashSet<string> Names = [ "eq", "ne", "gt", "lt", "ge", "le"];

  public IValueExpr Left => (IValueExpr)Operands[0];
  public IValueExpr Right => (IValueExpr)Operands[1];

  readonly SignalDef _signalDef;

  public static IExpression TryCreateFrom(ExpressionParser.Context context, string name, IList<IExpression> arguments) {
    return Names.Contains(name) ? new BinaryOperator(context, name, arguments) : null;
  }

  /// <inheritdoc/>
  public override string Describe() {
    var leftSignal = Left as SignalOperator;
    var rightSignal = Right as SignalOperator;
    if (leftSignal != null && rightSignal != null && leftSignal.SignalName == rightSignal.SignalName && Name == "eq") {
      return leftSignal.Describe();
    }
    var sb = new StringBuilder();
    sb.Append(leftSignal != null ? Left.Describe() : Left.ValueFn().FormatValue(_signalDef?.Result));
    sb.Append(Name switch {
        "eq" => " = ",
        "ne" => " \u2260 ",
        "gt" => " > ",
        "lt" => " < ",
        "ge" => " \u2265 ",
        "le" => " \u2264 ",
        _ => throw new InvalidOperationException("Unknown operator: " + Name),
    });
    sb.Append(rightSignal != null ? Right.Describe() : Right.ValueFn().FormatValue(_signalDef?.Result));
    return sb.ToString();
  }

  BinaryOperator(ExpressionParser.Context context, string name, IList<IExpression> operands)
      : base(name, operands) {
    AsserNumberOfOperandsExact(2);
    if (Operands[0] is not IValueExpr left) {
      throw new ScriptError.ParsingError("Left operand must be a value, found: " + Operands[0]);
    }
    if (Operands[1] is not IValueExpr right) {
      throw new ScriptError.ParsingError("Right operand must be a value, found: " + Operands[1]);
    }
    if (left.ValueType != right.ValueType) {
      throw new ScriptError.ParsingError($"Arguments type mismatch: {left.ValueType} != {right.ValueType}");
    }
    if (left is SignalOperator leftSignal) {
      _signalDef = context.ScriptingService.GetSignalDefinition(leftSignal.SignalName, context.ScriptHost);
    } else if (right is SignalOperator rightSignal) {
      _signalDef = context.ScriptingService.GetSignalDefinition(rightSignal.SignalName, context.ScriptHost);
    }
    if (_signalDef != null) {
      var otherArgExpr = left is SignalOperator ? right : left;
      _signalDef.Result.ArgumentValidator?.Invoke(otherArgExpr);
      var constantValueExpr = VerifyConstantValueExpr(_signalDef.Result, otherArgExpr);

      // Options compatibility support.
      if (constantValueExpr != null && _signalDef.Result.CompatibilityOptions != null) {
        var value = constantValueExpr.ValueFn().AsString;
        if (_signalDef.Result.CompatibilityOptions.TryGetValue(value, out var replaceOption)) {
          constantValueExpr = ConstantValueExpr.TryCreateFrom($"'{replaceOption}'");
          DebugEx.Warning("BinaryOperator: Replacing constant value '{0}' with '{1}' for signal {2}",
                          value, replaceOption, _signalDef.ScriptName);
          if (Operands[0] == otherArgExpr) {
            Operands[0] = constantValueExpr;
            left = constantValueExpr;
          } else {
            Operands[1] = constantValueExpr;
            right = constantValueExpr;
          }
        }
      }

      if (constantValueExpr != null && _signalDef.Result.Options != null
          && ScriptEngineSettings.CheckOptionsArguments) {
        var value = constantValueExpr.ValueFn().AsString;
        var allowedValues = _signalDef.Result.Options.Select(x => x.Value).ToArray();
        if (!allowedValues.Contains(value)) {
          throw new ScriptError.ParsingError($"Unexpected value: {value}. Allowed: {string.Join(", ", allowedValues)}");
        }
      }
    }
    Execute = left.ValueType switch {
        ScriptValue.TypeEnum.String => name switch {
            "eq" => () => left.ValueFn().AsString == right.ValueFn().AsString,
            "ne" => () => left.ValueFn().AsString != right.ValueFn().AsString,
            _ => throw new ScriptError.ParsingError("Unsupported operator for string operands: " + name),
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
