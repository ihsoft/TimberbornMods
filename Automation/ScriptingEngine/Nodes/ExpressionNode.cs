// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine.Nodes;

abstract class ExpressionNode {
  /// <summary>Evaluates the expression node.</summary>
  public abstract IExpressionValue Eval();
}