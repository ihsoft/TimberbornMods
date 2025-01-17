// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class SignalOperatorExpr : AbstractOperandExpr, IValueExpr {
  public ScriptValue.ValueType Type => _source.Type == ITriggerSource.ValueType.String
      ? ScriptValue.ValueType.String
      : ScriptValue.ValueType.Number;

  //FIXME: type depends on the signal. should be dynamic
  //FIXME: get script value from the soucre directly.
  public Func<ScriptValue> ValueFn {
    get { return () => ScriptValue.Of(_source.NumberValue); }
  }

  readonly ITriggerSource _source;
  
  public static IExpression TryCreateFrom(
      string name, IList<IExpression> operands, ExpressionParser parser) {
    return name == "sig" ? new SignalOperatorExpr(name, operands, parser) : null;
  }

  static readonly Regex SignalNameRegexp = new("^([a-zA-Z][a-zA-Z0-9]+)(.[a-zA-Z][a-zA-Z0-9]+)*$");

  SignalOperatorExpr(string name, IList<IExpression> operands, ExpressionParser parser)
      : base(name, operands) {
    AsserNumberOfOperandsExact(1);
    if (Operands[0] is not SymbolExpr symbol || !SignalNameRegexp.IsMatch(symbol.Value)) {
      throw new ScriptError("Bad signal name: " + Operands[0]);
    }
    _source = parser.GetSignalSource(symbol.Value);
  }
}
