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

  public static IExpression TryCreateFrom(string name, IList<IExpression> operands) {
    return name == "act" ? new ActionExpr(name, operands) : null;
  }

  /// <inheritdoc/>
  public override string Describe() {
    var def = ExpressionParser.Instance.GetActionDefinition(ActionName);
    var args = new List<object>();
    for (var i = 0; i < def.Arguments.Length; i++) {
      var argValue = Operands[i + 1];
      if (argValue is ConstantValueExpr constantValueExpr) {
        args.Add(constantValueExpr.FormatValue(def.Arguments[i]));
      } else {
        args.Add(argValue.Describe());
      }
    }
    return string.Format(def.DisplayName, args.ToArray());
  }

  static readonly Regex SignalNameRegexp = new("^([a-zA-Z][a-zA-Z0-9]+)(.[a-zA-Z][a-zA-Z0-9]+)*$");

  ActionExpr(string name, IList<IExpression> operands) : base(name, operands) {
    if (Operands[0] is not SymbolExpr symbol || !SignalNameRegexp.IsMatch(symbol.Value)) {
      throw new ScriptError("Bad action name: " + Operands[0]);
    }
    var actionName = symbol.Value;
    var def = ExpressionParser.Instance.GetActionDefinition(actionName);
    AsserNumberOfOperandsExact(def.Arguments.Length + 1);
    var argValues = new Func<ScriptValue>[def.Arguments.Length];
    for (var i = 0; i < def.Arguments.Length; i++) {
      var operand = Operands[i + 1];
      if (operand is not IValueExpr valueExpr) {
        throw new ScriptError($"Operand #{i + 1} must be a value, found: {operand}");
      }
      var argDef = def.Arguments[i];
      if (argDef.ValueType != valueExpr.ValueType) {
        throw new ScriptError($"Incompatible argument type #{i + 1}: {valueExpr.ValueType} is not {argDef.ValueType}");
      }
      argValues[i] = valueExpr.ValueFn;
    }
    var action = ExpressionParser.Instance.GetActionExecutor(actionName);
    Execute = () => action(argValues.Select(v => v()).ToArray());
  }
}
