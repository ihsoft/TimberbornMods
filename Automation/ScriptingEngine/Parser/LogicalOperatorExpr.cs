// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class LogicalOperatorExpr : BoolOperatorExpr {

  const string AndOperatorLocKey = "IgorZ.Automation.Scripting.Expressions.AndOperator";
  const string OrOperatorLocKey = "IgorZ.Automation.Scripting.Expressions.OrOperator";
  const string AndOperatorName = "and";
  const string OrOperatorName = "or";

  public static IExpression TryCreateFrom(string name, IList<IExpression> arguments) {
    return name is AndOperatorName or OrOperatorName ? new LogicalOperatorExpr(name, arguments) : null;
  }

  /// <inheritdoc/>
  public override string Describe() {
    var displayName = Name switch {
        AndOperatorName => ExpressionParser.Instance.Loc.T(AndOperatorLocKey),
        OrOperatorName => ExpressionParser.Instance.Loc.T(OrOperatorLocKey),
        _ => throw new InvalidOperationException("Unknown operator: " + Name),
    };
    var descriptions = new List<string>();
    foreach (var operand in Operands) {
      if (Name == AndOperatorName && operand is LogicalOperatorExpr { Name: OrOperatorName } logicalOperatorExpr) {
        descriptions.Add($"({logicalOperatorExpr.Describe()})");
      } else {
        descriptions.Add(operand.Describe());
      }
    }
    return string.Join(displayName, descriptions);
  }

  LogicalOperatorExpr(string name, IList<IExpression> operands) : base(name, operands) {
    AsserNumberOfOperandsRange(2, -1);
    var boolOperands = new List<BoolOperatorExpr>();
    for (var i = 0; i < operands.Count; i++) {
      var op = Operands[i];
      if (op is not BoolOperatorExpr result) {
        throw new ScriptError($"Operand #{i + 1} must be a boolean value, found: {op}");
      }
      boolOperands.Add(result);
    }
    Execute = name switch {
        AndOperatorName => () => boolOperands.All(x => x.Execute()),
        OrOperatorName => () => boolOperands.Any(x => x.Execute()),
        _ => throw new InvalidOperationException("Unknown operator: " + name),
    };
  }
}
