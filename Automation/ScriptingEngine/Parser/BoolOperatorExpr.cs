// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Text;

namespace IgorZ.Automation.ScriptingEngine.Parser;

//FIXME: deprecate. work with number expressions instead
abstract class BoolOperatorExpr(string name, IList<IExpression> operands) : AbstractOperandExpr(name, operands) {
  public Func<bool> Execute { get; protected init; }

  //FIXME: make serialize method
  public override string ToString() {
    var sb = new StringBuilder();
    sb.Append("(");
    sb.Append(Name);
    foreach (var operand in Operands) {
      sb.Append(" ");
      sb.Append(operand);
    }
    sb.Append(")");
    return sb.ToString();
  }
}