// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngineUI;
using IgorZ.Automation.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.Localization;

namespace Automation.Tests;

static class ExpressionDescriberTests {

  public static void ShowsOptionalLogicalParentheses() {
    EntityPanelSettings.ShowOptionalLogicalParentheses = true;

    Assert.Equal("(0.01 = 0.01 and 0.02 = 0.02) or 0.03 = 0.03", Describer().DescribeExpression(Expression()));
  }

  public static void HidesOptionalLogicalParentheses() {
    EntityPanelSettings.ShowOptionalLogicalParentheses = false;

    Assert.Equal("0.01 = 0.01 and 0.02 = 0.02 or 0.03 = 0.03", Describer().DescribeExpression(Expression()));
  }

  public static void DescribesBooleanComparisonAsOnAndOff() {
    var scriptable = new TestScriptable("Boolean");
    scriptable.RegisterSignal(
        "Boolean.State", ScriptValue.TypeEnum.Number,
        numericFormat: ValueDef.NumericFormatEnum.Boolean,
        options: [
            new DropdownItem { Value = "100", Text = "On" },
            new DropdownItem { Value = "0", Text = "Off" },
        ]);
    TestScripting.CreateService(scriptable);
    var context = new ExpressionContext { ScriptHost = new AutomationBehavior() };
    var onComparison = ComparisonOperator.CreateNe(
        context, [SignalOperator.Create(context, "Boolean.State"), Constant(0)]);
    var offComparison = ComparisonOperator.CreateEq(
        context, [SignalOperator.Create(context, "Boolean.State"), Constant(0)]);

    Assert.Equal("Boolean.State = On", Describer().DescribeExpression(onComparison));
    Assert.Equal("Boolean.State = Off", Describer().DescribeExpression(offComparison));
  }

  public static void RefreshesDynamicSignalDisplayName() {
    var displayName = "First name";
    var scriptable = new TestScriptable("Dynamic");
    scriptable.RegisterSignal(
        "Dynamic.State", ScriptValue.TypeEnum.Number,
        numericFormat: ValueDef.NumericFormatEnum.Boolean,
        options: [
            new DropdownItem { Value = "100", Text = "On" },
            new DropdownItem { Value = "0", Text = "Off" },
        ],
        displayNameFn: () => displayName);
    TestScripting.CreateService(scriptable);
    var context = new ExpressionContext { ScriptHost = new AutomationBehavior() };
    var comparison = ComparisonOperator.CreateNe(
        context, [SignalOperator.Create(context, "Dynamic.State"), Constant(0)]);

    Assert.Equal("First name = On", Describer().DescribeExpression(comparison));

    displayName = "Second name";

    Assert.Equal("Second name = On", Describer().DescribeExpression(comparison));
  }

  static ExpressionDescriber Describer() {
    return new ExpressionDescriber(new TestLoc());
  }

  static LogicalOperator Expression() {
    return LogicalOperator.CreateOr([
        LogicalOperator.CreateAnd([Comparison(1), Comparison(2)]),
        Comparison(3),
    ]);
  }

  static ComparisonOperator Comparison(int value) {
    return ComparisonOperator.CreateEq(null, [Constant(value), Constant(value)]);
  }

  static ConstantValueExpr Constant(int value) {
    return ConstantValueExpr.CreateFromValue(ScriptValue.Of(value));
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key switch {
          "IgorZ.Automation.Scripting.Expressions.AndOperator" => "and",
          "IgorZ.Automation.Scripting.Expressions.OrOperator" => "or",
          _ => key,
      };
    }
  }
}
