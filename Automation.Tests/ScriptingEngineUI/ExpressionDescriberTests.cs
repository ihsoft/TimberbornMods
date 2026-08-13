// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngineUI;
using IgorZ.Automation.Settings;
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
