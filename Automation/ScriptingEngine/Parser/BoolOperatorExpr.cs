// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Text;
using IgorZ.Automation.UI;

namespace IgorZ.Automation.ScriptingEngine.Parser;

abstract class BoolOperatorExpr : IExpression {
  public string Name  { get; protected init; }
  public Func<bool> Execute { get; protected init; }
  public IList<IExpression> Operands { get; init; }

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

  protected BoolOperatorExpr(string name, IList<IExpression> operands, int numberOfOperands) {
    AsserNumberOfOperands(name, operands, numberOfOperands);
    Name = name;
    Operands = operands;
  }
}