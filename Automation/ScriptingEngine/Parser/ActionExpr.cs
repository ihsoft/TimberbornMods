// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class ActionExpr : AbstractOperandExpr {
  public readonly Action Execute;

  public static IExpression TryCreateFrom(string name, IList<IExpression> operands, ExpressionParser parser) {
    return name == "act" ? new ActionExpr(name, operands, parser) : null;
  }

  static readonly Regex SignalNameRegexp = new("^([a-zA-Z][a-zA-Z0-9]+)(.[a-zA-Z][a-zA-Z0-9]+)*$");

  ActionExpr(string name, IList<IExpression> operands, ExpressionParser parser) : base(name, operands) {
    if (Operands[0] is not SymbolExpr symbol || !SignalNameRegexp.IsMatch(symbol.Value)) {
      throw new ScriptError("Bad action name: " + Operands[0]);
    }
    var actionName = symbol.Value;
    var def = parser.GetActionDefinition(actionName);
    AsserNumberOfOperandsExact(def.ArgumentTypes.Length + 1);
    var argValues = new Func<ScriptValue>[def.ArgumentTypes.Length];
    for (var i = 0; i < def.ArgumentTypes.Length; i++) {
      var operand = Operands[i + 1];
      if (operand is not IValueExpr valueExpr) {
        throw new ScriptError($"Operand #{i + 1} must be a value: {operand}");
      }
      var argDef = def.ArgumentTypes[i];
      if (argDef.ValueType != valueExpr.ValueType) {
        throw new ScriptError($"Incompatible argument type #{i + 1}: {valueExpr.ValueType} is not {argDef.ValueType}");
      }
      argValues[i] = valueExpr.ValueFn;
    }
    var action = parser.GetAction(actionName);
    Execute = () => action(argValues.Select(v => v()).ToArray());
  }
}
