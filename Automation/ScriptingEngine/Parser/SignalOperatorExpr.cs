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
  public ScriptValue.TypeEnum ValueType { get; }

  /// <inheritdoc/>
  public Func<ScriptValue> ValueFn { get; }

  /// <inheritdoc/>
  public override string Describe() {
    return ExpressionParser.Instance.GetSignalDefinition(SignalName).DisplayName;
  }

  public static IExpression TryCreateFrom(string name, IList<IExpression> operands) {
    return name == "sig" ? new SignalOperatorExpr(name, operands) : null;
  }

  static readonly Regex SignalNameRegexp = new("^([a-zA-Z][a-zA-Z0-9]+)(.[a-zA-Z][a-zA-Z0-9]+)*$");

  SignalOperatorExpr(string name, IList<IExpression> operands) : base(name, operands) {
    AsserNumberOfOperandsExact(1);
    if (Operands[0] is not SymbolExpr symbol || !SignalNameRegexp.IsMatch(symbol.Value)) {
      throw new ScriptError("Bad signal name: " + Operands[0]);
    }
    var def = ExpressionParser.Instance.GetSignalDefinition(symbol.Value);
    ValueType = def.Result.ValueType;
    ValueFn = ExpressionParser.Instance.GetSignalSource(symbol.Value);
  }
}
