// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Text;
using IgorZ.Automation.Settings;

namespace IgorZ.Automation.ScriptingEngine.Parser;

abstract class AbstractOperator(string name, IList<IExpression> operands) : IExpression {

  public readonly string Name = name;
  public readonly IList<IExpression> Operands = operands;

  /// <inheritdoc/>
  public string Serialize() {
    var sb = new StringBuilder();
    sb.Append("(");
    sb.Append(Name);
    foreach (var operand in Operands) {
      sb.Append(" ");
      sb.Append(operand.Serialize());
    }
    sb.Append(")");
    return sb.ToString();
  }

  /// <inheritdoc/>
  public abstract string Describe();

  /// <inheritdoc/>
  public void VisitNodes(Action<IExpression> visitorFn) {
    visitorFn(this);
    foreach (var expression in Operands) {
      expression.VisitNodes(visitorFn);
    }
  }

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType().Name}#{Serialize()}";
  }

  protected void AsserNumberOfOperandsExact(int expected) {
    var count = Operands.Count;
    if (expected != count) {
      throw new ScriptError.ParsingError($"Operator '{Name}' requires {expected} arguments, but got {count}");
    }
  }

  protected void AsserNumberOfOperandsRange(int min, int max) {
    var count = Operands.Count;
    if (min >= 0 && count < min) {
      throw new ScriptError.ParsingError($"Operator '{Name}' requires at least {min} arguments, but got {count}");
    }
    if (max >= 0 && count > max) {
      throw new ScriptError.ParsingError($"Operator '{Name}' requires at most {max} arguments, but got {count}");
    }
  }

  protected static ConstantValueExpr VerifyConstantValueExpr(ValueDef valueDef, IValueExpr valueExpr) {
    if (valueExpr is not ConstantValueExpr constantValueExpr) {
      return null;
    }
    if (!ScriptEngineSettings.CheckArgumentValues) {
      return constantValueExpr;
    }
    try {
      valueDef.ValueValidator?.Invoke(valueExpr.ValueFn());
    } catch (ScriptError e) {
      // Report as parsing error since it is checked at parse time.
      throw new ScriptError.ParsingError(e.Message);
    }
    return constantValueExpr;
  }
}
