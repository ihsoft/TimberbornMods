// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Parser;

interface IExpression {
  public string Serialize();
  public string Describe();
  public void VisitNodes(Action<IExpression> visitorFn);
}
