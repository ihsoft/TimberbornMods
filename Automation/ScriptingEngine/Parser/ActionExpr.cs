// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace IgorZ.Automation.ScriptingEngine.Parser;

sealed class ActionExpr : AbstractOperandExpr {

  public string ActionName => ((SymbolExpr)Operands[0]).Value;
  public readonly Action Execute;

  readonly ActionDef _actionDef;

  public static IExpression TryCreateFrom(ExpressionParser.Context context, string name, IList<IExpression> operands) {
    return name == "act" ? new ActionExpr(context, name, operands) : null;
  }

  /// <inheritdoc/>
  public override string Describe() {
    var args = new string[_actionDef.Arguments.Length];
    for (var i = 0; i < _actionDef.Arguments.Length; i++) {
      var value = (Operands[i + 1] as IValueExpr)!.ValueFn();
      args[i] = value.FormatValue(_actionDef.Arguments[i]);
    }
    return string.Format(_actionDef.DisplayName, args);
  }

  static readonly Regex SignalNameRegexp = new("^([a-zA-Z][a-zA-Z0-9]+)(.[a-zA-Z][a-zA-Z0-9]+)*$");

  ActionExpr(ExpressionParser.Context context, string name, IList<IExpression> operands) : base(name, operands) {
    if (Operands[0] is not SymbolExpr symbol || !SignalNameRegexp.IsMatch(symbol.Value)) {
      throw new ScriptError("Bad action name: " + Operands[0]);
    }
    var actionName = symbol.Value;
    if (context.IsPreprocessor) {
      throw new ScriptError("Actions are not allowed in preprocessor: " + actionName);
    }
    context.ReferencedActions.Add(actionName);
    _actionDef = context.ScriptingService.GetActionDefinition(actionName, context.ScriptHost);
    AsserNumberOfOperandsExact(_actionDef.Arguments.Length + 1);
    var argValues = new Func<ScriptValue>[_actionDef.Arguments.Length];
    for (var i = 0; i < _actionDef.Arguments.Length; i++) {
      var operand = Operands[i + 1];
      if (operand is not IValueExpr valueExpr) {
        throw new ScriptError($"Operand #{i + 1} must be a value, found: {operand}");
      }
      var argDef = _actionDef.Arguments[i];
      if (argDef.ValueType != valueExpr.ValueType) {
        throw new ScriptError($"Incompatible argument type #{i + 1}: {valueExpr.ValueType} is not {argDef.ValueType}");
      }
      argValues[i] = valueExpr.ValueFn;
    }
    var action = context.ScriptingService.GetActionExecutor(actionName, context.ScriptHost);
    Execute = () => action(argValues.Select(v => v()).ToArray());
  }
}
