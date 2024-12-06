// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

sealed class ConstantValueNode(IExpressionValue value) : ExpressionNode {
  /// <inheritdoc/>
  public override IExpressionValue Eval() => value;
}