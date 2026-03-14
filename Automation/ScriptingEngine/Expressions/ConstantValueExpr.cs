// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

sealed class ConstantValueExpr : IValueExpr {

  public ScriptValue.TypeEnum ValueType { get; private init; }
  public Func<ScriptValue> ValueFn { get; private init; }

  public static ConstantValueExpr CreateStringLiteral(string literal) {
    return new ConstantValueExpr {
        ValueType = ScriptValue.TypeEnum.String,
        ValueFn = () => ScriptValue.FromString(literal),
    };
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

  /// <summary>
  /// Validates the constant value against the value definition. For string values it also checks the options and
  /// compatibility support.
  /// </summary>
  /// <remarks>
  /// Constant values can be checked for the constraints defined in the value definition at parsing stage. And for the
  /// string values we can also check the options and compatibility support. This method does all these checks and
  /// returns the corrected value if needed. The corrected value is a substitution of the old constant value, it should
  /// be used in the expression instead of the old one. It's the caller's responsibility to replace the old value with
  /// the new one in the expression tree.
  /// </remarks>
  /// <param name="valueDef">The value definition.</param>
  /// <param name="fixedValueExpr">
  /// The variable that receives the new constant expression if the value was corrected due to compatibility options.
  /// It will be <c>null</c> if this method returns <c>false</c>.
  /// </param>
  /// <returns>
  /// <c>true</c> if value was corrected. The corrected value is stored in <paramref name="fixedValueExpr"/>.
  /// </returns>
  /// <exception cref="ScriptError.ParsingError">if the constant doesn't pass validation.</exception>
  public bool ValidateAndMaybeCorrect(ValueDef valueDef, out IValueExpr fixedValueExpr) {
    if (ValueType == ScriptValue.TypeEnum.Unset) {
      throw new InvalidOperationException($"Cannot validate unset value: {this}");
    }

    var res = false;
    fixedValueExpr = null;

    // String values can have options and compatibility support.
    if (ValueType == ScriptValue.TypeEnum.String) {
      var value = ValueFn().AsString;

      // For the options case, the deprecated value can be mapped to the new.
      if (valueDef.CompatibilityOptions != null
          && valueDef.CompatibilityOptions.TryGetValue(value, out var replaceOption)) {
        fixedValueExpr = CreateStringLiteral(replaceOption);
        value = replaceOption;
        res = true;
      }

      if (valueDef.Options != null) {
        var allowedValues = valueDef.Options.Select(x => x.Value).ToArray();
        if (!allowedValues.Contains(value)) {
          throw new ScriptError.ParsingError($"Unexpected value: {value}. Allowed: {string.Join(", ", allowedValues)}");
        }
      }
    }

    // Validator normally checks the value at runtime, but for constant values we can check it once at parsing stage and
    // report as parsing error.
    try {
      valueDef.RuntimeValueValidator?.Invoke(fixedValueExpr != null ? fixedValueExpr.ValueFn() : ValueFn());
    } catch (ScriptError.RuntimeError e) {
      throw new ScriptError.ParsingError(e.Message);
    }

    return res;
  }
}
