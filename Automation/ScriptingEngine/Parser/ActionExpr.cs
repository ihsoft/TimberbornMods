// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.UI;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class ActionExpr : IExpression {
  public string Name { get; init; }
  public IExpression[] Arguments { get; init; }

  public void Execute() {
    //FIXME: implement;
  }

  internal ActionExpr(string name, IExpression[] arguments) {
    Name = name;
    Arguments = arguments;
  }
}
