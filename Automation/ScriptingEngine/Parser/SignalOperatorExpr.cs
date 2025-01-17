// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class SignalOperatorExpr : AbstractOperandExpr, IValueExpr {
  public IValueExpr.ValueType Type => _source.Type == ITriggerSource.ValueType.String
      ? IValueExpr.ValueType.String
      : IValueExpr.ValueType.Number;

  public Func<string> GetStringValue {
    get { return () => _source.StringValue; }
  }

  public Func<int> GetNumberValue {
    get { return () => _source.NumberValue; }
  }

  readonly ITriggerSource _source;
  
  public static IExpression TryCreateFrom(
      string name, IList<IExpression> operands, Func<string, ITriggerSource> signalSourceProvider) {
    return name == "sig" ? new SignalOperatorExpr(name, operands, signalSourceProvider) : null;
  }

  static readonly Regex SignalNameRegexp = new("^([a-zA-Z][a-zA-Z0-9]+)(.[a-zA-Z][a-zA-Z0-9]+)*$");

  SignalOperatorExpr(string name, IList<IExpression> operands, Func<string, ITriggerSource> signalSourceProvider)
      : base(name, operands) {
    AsserNumberOfOperandsExact(1);
    if (Operands[0] is not SymbolExpr symbol || !SignalNameRegexp.IsMatch(symbol.Value)) {
      throw new ScriptError("Bad signal name: " + Operands[0]);
    }
    _source = signalSourceProvider(symbol.Value);
  }
}
