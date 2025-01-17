// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine.Parser;

class SymbolExpr : IExpression {
  public string Value { get; init; }
  public override string ToString() => Value;
}
