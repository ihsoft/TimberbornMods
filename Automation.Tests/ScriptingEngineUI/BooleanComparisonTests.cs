using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngineUI;

namespace Automation.Tests;

static class BooleanComparisonTests {
  public static void RecognizesCanonicalOnAndOffComparisons() {
    var context = Context();

    var isOff = BooleanComparison.TryGetState(
        ComparisonOperator.CreateEq(context, [Signal(context), Constant(0)]), out var offState);
    var isOn = BooleanComparison.TryGetState(
        ComparisonOperator.CreateNe(context, [Signal(context), Constant(0)]), out var onState);

    Assert.True(isOff);
    Assert.False(offState);
    Assert.True(isOn);
    Assert.True(onState);
  }

  public static void AcceptsRecommendedTrueValueForExistingRules() {
    var context = Context();

    var recognized = BooleanComparison.TryGetState(
        ComparisonOperator.CreateEq(context, [Signal(context), Constant(100)]), out var state);

    Assert.True(recognized);
    Assert.True(state);
  }

  public static void RejectsComparisonsThatDoNotRepresentBooleanState() {
    var context = Context();

    Assert.False(BooleanComparison.TryGetState(
        ComparisonOperator.CreateEq(context, [Signal(context), Constant(200)]), out _));
    Assert.False(BooleanComparison.TryGetState(
        ComparisonOperator.CreateNe(context, [Signal(context), Constant(100)]), out _));
    Assert.False(BooleanComparison.TryGetState(
        ComparisonOperator.CreateGt(context, [Signal(context), Constant(0)]), out _));
  }

  static ExpressionContext Context() {
    var scriptable = new TestScriptable("Boolean");
    scriptable.RegisterSignal(
        "Boolean.State", ScriptValue.TypeEnum.Number,
        numericFormat: ValueDef.NumericFormatEnum.Boolean);
    TestScripting.CreateService(scriptable);
    return new ExpressionContext { ScriptHost = new AutomationBehavior() };
  }

  static SignalOperator Signal(ExpressionContext context) {
    return SignalOperator.Create(context, "Boolean.State");
  }

  static ConstantValueExpr Constant(int rawValue) {
    return ConstantValueExpr.CreateFromValue(ScriptValue.Of(rawValue));
  }
}
