// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

abstract class BooleanOperator(IList<IExpression> operands) : AbstractOperator(operands) {
  public Func<bool> Execute { get; protected init; }
}
