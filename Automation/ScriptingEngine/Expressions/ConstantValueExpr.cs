// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

sealed class ConstantValueExpr : IValueExpr {

  public ScriptValue.TypeEnum ValueType { get; private init; }
  public Func<ScriptValue> ValueFn { get; private init; }

  public static ConstantValueExpr CreateStringLiteral(string literal) {
    return new ConstantValueExpr { ValueType = ScriptValue.TypeEnum.String, ValueFn = () => ScriptValue.FromString(literal) };
  }

  public static ConstantValueExpr CreateFromValue(ScriptValue value) {
    return new ConstantValueExpr { ValueType = value.ValueType, ValueFn = () => value };
  }

  /// <inheritdoc/>
  public void VisitNodes(Action<IExpression> visitorFn) {
    visitorFn(this);
  }

  /// <inheritdoc/>
  public override string ToString() {
    return ValueFn().ToString();
  }
}
