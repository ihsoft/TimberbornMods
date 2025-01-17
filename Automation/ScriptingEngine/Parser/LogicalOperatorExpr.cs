// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class LogicalOperatorExpr : BoolOperatorExpr {
  static readonly HashSet<string> Names = ["and", "or"];

  public static IExpression TryCreateFrom(string name, IList<IExpression> arguments) {
    return Names.Contains(name) ? new LogicalOperatorExpr(name, arguments) : null;
  }

  LogicalOperatorExpr(string name, IList<IExpression> operands) : base(name, operands) {
    AsserNumberOfOperandsRange(1, -1);
    var boolOperands = new List<BoolOperatorExpr>();
    for (var i = 0; i < operands.Count; i++) {
      var op = Operands[i];
      if (op is not BoolOperatorExpr result) {
        throw new ScriptError($"Operand #{i + 1} must be a boolean value: {op}");
      }
      boolOperands.Add(result);
    }
    Execute = name switch {
        "and" => () => boolOperands.All(x => x.Execute()),
        "or" => () => boolOperands.Any(x => x.Execute()),
        _ => throw new InvalidOperationException("Unknown operator: " + name),
    };
  }
}
