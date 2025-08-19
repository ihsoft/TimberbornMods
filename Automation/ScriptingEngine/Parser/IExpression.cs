// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Parser;

interface IExpression {
  /// <summary>Returns a string that can be parsed back to the rule.</summary>
  public string Serialize();

  /// <summary>Returns a human-friendly description of the expression.</summary>
  public string Describe();

  /// <summary>Visits all nodes in the expression tree and applies the visitor function to each node.</summary>
  public void VisitNodes(Action<IExpression> visitorFn);
}
