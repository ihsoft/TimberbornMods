// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine.Parser;

interface IExpression {
  public string Serialize();
  public string Describe();
}
