// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;

namespace IgorZ.Automation.ScriptingEngine.Parser;

abstract class BoolOperatorExpr(string name, IList<IExpression> operands) : AbstractOperandExpr(name, operands) {
  public Func<bool> Execute { get; protected init; }
}
