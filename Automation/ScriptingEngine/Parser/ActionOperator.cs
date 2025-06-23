// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.Automation.Settings;
using TimberApi.DependencyContainerSystem;
using Timberborn.Localization;

namespace IgorZ.Automation.ScriptingEngine.Parser;

sealed class ActionOperator : AbstractOperator {

  const string ActOnceNameSuffix = ".Once";

  public string FullActionName => ((SymbolExpr)Operands[0]).Value;
  public readonly string ActionName;
  public readonly bool ExecuteOnce; 
  public readonly Action Execute;

  readonly ActionDef _actionDef;

  public static IExpression TryCreateFrom(ExpressionParser.Context context, string name, IList<IExpression> operands) {
    return name == "act" ? new ActionOperator(context, name, operands) : null;
  }

  /// <inheritdoc/>
  public override string Describe() {
    var args = new string[_actionDef.Arguments.Length];
    for (var i = 0; i < _actionDef.Arguments.Length; i++) {
      var operand = Operands[i + 1] as IValueExpr;
      if (EntityPanelSettings.EvalValuesInActionArguments) {
        ScriptValue value;
        try {
          value = operand!.ValueFn();
        } catch (ScriptError.BadValue e) {
          return DependencyContainer.GetInstance<ILoc>().T(e.LocKey);
        }
        args[i] = value.FormatValue(_actionDef.Arguments[i]);
      } else {
        args[i] = operand!.Describe();
      }
    }
    return string.Format(_actionDef.DisplayName, args);
  }

  static readonly Regex ActionNameRegexp = new("^([a-zA-Z][a-zA-Z0-9]+)(.[a-zA-Z][a-zA-Z0-9]+)*$");

  ActionOperator(ExpressionParser.Context context, string name, IList<IExpression> operands) : base(name, operands) {
    if (Operands[0] is not SymbolExpr symbol || !ActionNameRegexp.IsMatch(symbol.Value)) {
      throw new ScriptError.ParsingError("Bad action name: " + Operands[0]);
    }
    var actionName = symbol.Value;
    if (actionName.EndsWith(ActOnceNameSuffix)) {
      ExecuteOnce = true;
      actionName = actionName[..^ActOnceNameSuffix.Length];
    }
    ActionName = actionName;
    _actionDef = context.ScriptingService.GetActionDefinition(ActionName, context.ScriptHost);
    AsserNumberOfOperandsExact(_actionDef.Arguments.Length + 1);
    var argValues = new Func<ScriptValue>[_actionDef.Arguments.Length];
    for (var i = 0; i < _actionDef.Arguments.Length; i++) {
      var operand = Operands[i + 1];
      if (operand is not IValueExpr valueExpr) {
        throw new ScriptError.ParsingError($"Argument #{i + 1} must be a value, but found: {operand}");
      }
      var argDef = _actionDef.Arguments[i];
      if (argDef.ValueType != valueExpr.ValueType) {
        throw new ScriptError.ParsingError(
            $"Argument #{i + 1} must be of type '{argDef.ValueType}', but found: {valueExpr.ValueType}");
      }
      argDef.ArgumentValidator?.Invoke(valueExpr);
      if (argDef.ValueValidator == null || VerifyConstantValueExpr(argDef, valueExpr) != null) {
        argValues[i] = valueExpr.ValueFn;
      } else {
        argValues[i] = () => {
          var value = valueExpr.ValueFn();
          if (ScriptEngineSettings.CheckArgumentValues) {
            argDef.ValueValidator(value);
          }
          return value;
        };
      }
    }
    var action = context.ScriptingService.GetActionExecutor(ActionName, context.ScriptHost);
    Execute = () => action(argValues.Select(v => v()).ToArray());
  }
}
