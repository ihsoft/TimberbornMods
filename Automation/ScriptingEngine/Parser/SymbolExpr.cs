// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class SymbolExpr : IExpression {

  public string Value { get; init; }

  /// <inheritdoc/>
  public string Serialize() {
    return Value;
  }

  /// <inheritdoc/>
  public string Describe() {
    throw new System.NotImplementedException();
  }

  /// <inheritdoc/>
  public void VisitNodes(Action<IExpression> visitorFn) {
    visitorFn(this);
  }

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType().Name}#{Serialize()}";
  }
}
