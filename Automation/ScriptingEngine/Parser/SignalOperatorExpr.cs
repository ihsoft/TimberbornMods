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

  readonly SignalDef _signalDef;

  /// <inheritdoc/>
  public override string Describe() {
    return _signalDef.DisplayName;
  }

  public static IExpression TryCreateFrom(ExpressionParser.Context context, string name, IList<IExpression> operands) {
    return name == "sig" ? new SignalOperatorExpr(context, name, operands) : null;
  }

  static readonly Regex SignalNameRegexp = new("^([a-zA-Z][a-zA-Z0-9]+)(.[a-zA-Z][a-zA-Z0-9]+)*$");

  SignalOperatorExpr(ExpressionParser.Context context, string name, IList<IExpression> operands)
      : base(name, operands) {
    AsserNumberOfOperandsExact(1);
    if (Operands[0] is not SymbolExpr symbol || !SignalNameRegexp.IsMatch(symbol.Value)) {
      throw new ScriptError("Bad signal name: " + Operands[0]);
    }
    _signalDef = context.ScriptingService.GetSignalDefinition(symbol.Value, context.ScriptHost);
    ValueType = _signalDef.Result.ValueType;
    ValueFn = context.ScriptingService.GetSignalSource(symbol.Value, context.ScriptHost);
  }
}
