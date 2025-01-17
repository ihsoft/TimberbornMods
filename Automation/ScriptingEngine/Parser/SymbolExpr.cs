// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine.Parser;

class SymbolExpr : IExpression {
  public string Value { get; init; }

  public string Serialize() {
    return Value;
  }

  public override string ToString() {
    return $"{GetType().Name}#{Serialize()}";
  }
}
