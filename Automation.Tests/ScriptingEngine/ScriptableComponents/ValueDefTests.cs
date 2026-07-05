using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.TimberDev.UI;

namespace Automation.Tests;

static class ValueDefTests {
  public static void RangeCheckValidatorAcceptsValuesInRange() {
    var validator = ValueDef.RangeCheckValidator(min: 1.5f, max: 2.5f);

    validator(ScriptValue.FromFloat(1.5f));
    validator(ScriptValue.FromFloat(2.0f));
    validator(ScriptValue.FromFloat(2.5f));
  }

  public static void RangeCheckValidatorRejectsValuesOutsideRange() {
    var validator = ValueDef.RangeCheckValidator(min: 1.5f, max: 2.5f);

    Assert.Throws<ScriptError.ValueOutOfRange>(() => validator(ScriptValue.FromFloat(1.49f)));
    Assert.Throws<ScriptError.ValueOutOfRange>(() => validator(ScriptValue.FromFloat(2.51f)));
  }

  public static void RangeCheckValidatorSupportsSingleBoundary() {
    var minValidator = ValueDef.RangeCheckValidator(min: 1.5f);
    var maxValidator = ValueDef.RangeCheckValidator(max: 2.5f);

    minValidator(ScriptValue.FromFloat(1.5f));
    maxValidator(ScriptValue.FromFloat(2.5f));
    Assert.Throws<ScriptError.ValueOutOfRange>(() => minValidator(ScriptValue.FromFloat(1.49f)));
    Assert.Throws<ScriptError.ValueOutOfRange>(() => maxValidator(ScriptValue.FromFloat(2.51f)));
  }

  public static void StringOptionsCanAllowCustomValues() {
    var valueDef = new ValueDef {
        ValueType = ScriptValue.TypeEnum.String,
        Options = [new DropdownItem { Value = "known", Text = "Known" }],
        AllowCustomOptions = true,
    };
    var expression = ConstantValueExpr.CreateStringLiteral("custom");

    Assert.False(expression.ValidateAndMaybeCorrect(valueDef, out var fixedValueExpr));
    Assert.Equal(null, fixedValueExpr);
  }
}
