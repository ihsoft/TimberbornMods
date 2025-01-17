using System.Collections.Generic;

namespace IgorZ.Automation.ScriptingEngine.Parser;

abstract class AbstractOperandExpr(string name, IList<IExpression> operands) : IExpression {
  public string Name { get; } = name;

  protected readonly IList<IExpression> Operands = operands;

  protected void AsserNumberOfOperandsExact(int expected) {
    var count = Operands.Count;
    if (expected != count) {
      throw new ScriptError($"Operator '{Name}' requires {expected} arguments, but got {count}");
    }
  }

  protected void AsserNumberOfOperandsRange(int min, int max) {
    var count = Operands.Count;
    if (min >= 0 && count < min) {
      throw new ScriptError($"Operator '{Name}' requires at least {min} arguments, but got {count}");
    }
    if (max >= 0 && count > max) {
      throw new ScriptError($"Operator '{Name}' requires at most {max} arguments, but got {count}");
    }
  }
}
