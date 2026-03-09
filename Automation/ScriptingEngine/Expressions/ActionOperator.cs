// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.Settings;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

sealed class ActionOperator : AbstractOperator {

  const string ActOnceNameSuffix = ".Once";

  public readonly string FullActionName;
  public readonly string ActionName;
  public readonly bool ExecuteOnce; 
  public readonly Action Execute;
  public readonly ActionDef ActionDef;

  public static ActionOperator Create(ExpressionContext context, string actionName, IList<IExpression> operands) {
    return new ActionOperator(context, actionName, operands);
  }

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType().Name}('{FullActionName}')";
  }

  ActionOperator(ExpressionContext context, string actionName, IList<IExpression> operands) : base(operands) {
    FullActionName = actionName;
    if (actionName.EndsWith(ActOnceNameSuffix)) {
      ExecuteOnce = true;
      actionName = actionName[..^ActOnceNameSuffix.Length];
    }
    ActionName = actionName;
    ActionDef = ScriptingService.Instance.GetActionDefinition(ActionName, context.ScriptHost);
    if (ActionDef.VarArg == null) {
      AssertNumberOfOperandsExact(ActionDef.Arguments.Length);
    } else {
      // Variable number of arguments allowed. Check for minimum counter only.
      AssertNumberOfOperandsRange(ActionDef.Arguments.Length, -1);
    }

    // Handle fixed position arguments.
    var argValues = new List<Func<ScriptValue>>(operands.Count);
    var argDefIndex = 0;
    for (var argPos = 0; argPos < operands.Count; argPos++) {
      var operand = operands[argPos];
      if (operand is not IValueExpr valueExpr) {
        throw new ScriptError.ParsingError($"Argument #{argPos + 1} must be a value, but found: {operand}");
      }
      var argDef = argDefIndex < ActionDef.Arguments.Length
          ? ActionDef.Arguments[argDefIndex++]
          : ActionDef.VarArg;
      // The unset type means that any type is allowed. It is mostly used for the variable arguments case.
      if (argDef.ValueType != ScriptValue.TypeEnum.Unset && argDef.ValueType != valueExpr.ValueType) {
        throw new ScriptError.ParsingError(
            $"Argument #{argPos + 1} must be of type '{argDef.ValueType}', but found: {valueExpr.ValueType}");
      }
      argDef.ArgumentValidator?.Invoke(valueExpr);
      if (valueExpr is ConstantValueExpr constantValueExpr) {
        if (constantValueExpr.ValidateAndMaybeCorrect(argDef, out var newValueExpr)) {
          DebugEx.Warning("ActionOperator: Replacing constant value '{0}' with '{1}' for {2}",
                          valueExpr.ValueFn(), newValueExpr.ValueFn(), actionName);
          valueExpr = newValueExpr;
          Operands[argPos] = newValueExpr;
        }
      }
      if (argDef.RuntimeValueValidator == null || valueExpr.IsConstantValue()) {
        argValues.Add(valueExpr.ValueFn);
      } else {
        argValues.Add(() => {
          var value = valueExpr.ValueFn();
          if (ScriptEngineSettings.CheckArgumentValues) {
            argDef.RuntimeValueValidator(value);
          }
          return value;
        });
      }
    }
    var action = ScriptingService.Instance.GetActionExecutor(ActionName, context.ScriptHost);
    Execute = () => action(argValues.Select(v => v()).ToArray());
  }
}
