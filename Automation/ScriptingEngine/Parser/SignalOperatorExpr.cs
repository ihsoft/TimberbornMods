// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace IgorZ.Automation.ScriptingEngine.Parser;

sealed class SignalOperatorExpr : AbstractOperandExpr, IValueExpr {
  public string SignalName => ((SymbolExpr)Operands[0]).Value;

  /// <inheritdoc/>
  public ScriptValue.TypeEnum ValueType { get; private set; }

  /// <inheritdoc/>
  public Func<ScriptValue> ValueFn {
    get { return () => _source.CurrentValue; }
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
    ValueType = _source.CurrentValue.ValueType;
  }
}
