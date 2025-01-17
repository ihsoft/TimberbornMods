// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.Automation.UI;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class SignalOperatorExpr : ValueExpr {
  public string Name { get; init; }
  public string Value { get; init; }
  public ITriggerSource Source { get; init; }
  
  public static IExpression TryCreate(string name, IList<IExpression> operands) {
    return name == "sig" ? new SignalOperatorExpr(name, operands) : null;
  }

  SignalOperatorExpr(string name, IList<IExpression> operands) {
    AsserNumberOfOperands(name, operands, 1);
    Name = name;
    //FIXME: get trigger and set value getter/type
    Type = ValueType.Number;
  }
}
