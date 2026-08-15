using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.ScriptingEngineUI;
using IgorZ.Automation.Settings;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;

namespace Automation.Tests;

static class ParserRegressionTests {
  static readonly string[] ValidPythonExpressions = [
      // String constants.
      "\"te'st\" == 'te\\'st'",
      "'te\"st' == \"te\\\"st\"",
      @"'te\\st' == 'te\\st'",
      "'test' != 'test2'",

      // Number constants.
      "-1.0 == -1",
      "12.0 == 12",
      "123.0 == 123",
      "-123.0 == -123",
      "123.0 - 10 == 113",

      // Custom signals.
      "Signals.Set('yellow', 12)",
      "Signals.Set('yellow', Signals.Var1 + 123)",

      // Actions.
      "Foobar.EmptyAction()",
      "Foobar.OneArgumentAction(1)",
      "Foobar.OneArgumentAction(Signals.Var1 + 123)",

      // Concat function.
      "concat(1, '-test-', 2) == '1-test-2'",
      "concat(Signals.Var1, '-test-', 1+2+3) == '0-test-6'",

      // Property functions.
      "getvalue('Foobar.str') == 'test'",
      "getvalue('Foobar.numInt') == 123",
      "getvalue('Foobar.numFloat') == 123.33",
      "getvalue('Foobar.boolFalse') == 0",
      "getvalue('Foobar.boolTrue') == 1",
      "getlen('Foobar.strList') == 2",
      "getelement('Foobar.strList', 1) == 'two'",
      "getelement('Foobar.numList', 1) == 2",

      // Multi-argument operators.
      "1 + 2 + (3 + 4)",
      "(1 + 2) + 3 + 4",
      "1 + 2 + (3 - 4)",
      "1 == 1 and 2 == 2 and (3 == 3 or 4 == 4)",
      "1 == 1 or 2 == 2 or 3 == 3 and 4 == 4",

      // Math equations.
      "100 >= -200",
      "1.5 * (20 / -5) == -6.00",
      "1.5 * -(20 / -5) == 6.00",
      "-1.5 * -(20 / -5) == -6.00",
      "--20 / 10 / 2 == 1",
      "-(-20 / 10) / 2 == 1",
      "--20 / (10 / 2) == 4",
      "1 - 2 - 3 == -4",
      "(1 - 2) - 3 == -4",
      "1 - (2 - 3) == 2",
      "1 - 2 > -2",
      "1 - 2 >= -1",
      "21 % 5 % 3 == 1",
      "(21 % 5) % 3 == 1",
      "21 % (5 % 3) == 1",
      "21 % 5 * 3 == 3",
      "(21 % 5) * 3 == 3",
      "21 % (5 * 3) == 6",
      "1.00 == 1",
      "1.01 == 1.01",
      "1 + 0.01 == 1.01",
      "1.00 + 0.00 == 1",
      "round(1.01) == 1",
      "round(1.61) == 2",
      "min(1,2,3) == 1",
      "max(1,2,3) == 3",
      "-1 == (0 - 1)",

      // Math functions.
      "max(12, 13, 14) == 14",
      "min(12, 13, 14) == 12",
      "max(10+4, 10+3, 10+2) == 14",
      "min(10+4, 10+3, 10+2) == 12",
      "round(1) == 1",
      "round(1.33) == 1",
      "round(1.55) == 2",
      "round(1/3) == 0",
      "round(4/3) == 1",
      "round(2/3) == 1",
      "round(5/3) == 2",

      // Variadic action.
      "Debug.Log('foo={0}, bar={1}', 1, 'test')",
  ];

  static readonly string[] InvalidPythonExpressions = [
      "1 + ()",
      "1 + ((1)",
      "1 + (1))",
      "1 + (1 + 2 + )",
      "1 + (1 + 2 + 3",
      "1 + (1 + 2 3)",
      "(1 + 2 ())",
      "'test' > 'test'",
      "'test' == 123",
      @"'te\st' == 'test'",
      "\"te\\st\" == 'test'",
      "'te\\\"st' == 'test'",
      "\"te\\'st\" == 'test'",
      "-.01",
      "01.abc",
      "Signals.1Var",
      "Signals.Set(1, 2 3)",
      "Signals.Set(1, 2, 3",
      "Signals.Set 1, 2, 3)",
      "max(12, 13, 14)/ (min (1-(2-3),2,3) / Test.Var1) + (Signals.Set(\"yellow1\", 34))",
      "(12 * 1 - 2) * 3 + 3 / 2 / (32 + 4) * 7 + \"te'st\" + loh.loh<=1",
      "getvalue('test')",
      "getvalue('Foobar.numInt') == 'foo'",
      "getvalue('Foobar.str') == 1",
      "getvalue('Foobar.strList')",
      "getelement('foobar.numList', 2)",
      "getelement('Foobar.numList', 1, 1)",
      "getelement(1, 0)",
      "getelement()",
      "getvalue()",
      "getvalue(Signals.Var1)",
      "concat()",
      "min(1)",
      "max(2)",
      "round()",

      // Numeric values outside the supported precision.
      "-1.",
      "12.",
      "123.-10",
      "1.001 == 1.00",
      "1.006 == 1.01",
      "1 + 0.006 == 1.01",
      "1.003 + 0.003 == 1",
      "round(1.333) == 1",
      "round(1.555) == 2",
  ];

  public static void ValidSamplesRoundTripDescribeAndExecute() {
    var behavior = CreateBehavior();
    var pythonParser = new PythonSyntaxParser();
    var lispParser = new LispSyntaxParser();
    var describer = new ExpressionDescriber(new TestLoc());
    EntityPanelSettings.ShowOptionalLogicalParentheses = true;

    AssertSamples(
        ValidPythonExpressions,
        sample => ValidateSample(sample, behavior, pythonParser, lispParser, describer));
  }

  public static void InvalidSamplesAreRejected() {
    var behavior = CreateBehavior();
    var parser = new PythonSyntaxParser();

    AssertSamples(InvalidPythonExpressions, sample => {
      var result = parser.Parse(sample, behavior);
      Assert.True(result.LastScriptError != null, "Expected parser sample to fail: " + sample);
      Assert.Equal(null, result.ParsedExpression);
    });
  }

  public static void CollectionElementReportsOutOfRangeIndex() {
    var behavior = CreateBehavior();
    var parser = new PythonSyntaxParser();
    var expression = ParseOk(parser, "getelement('Foobar.numList', 2) == 0", behavior);

    Assert.Throws<ScriptError.ValueOutOfRange>(() => ((BooleanOperator)expression).Execute());
  }

  static void ValidateSample(
      string sample, AutomationBehavior behavior, PythonSyntaxParser pythonParser, LispSyntaxParser lispParser,
      ExpressionDescriber describer) {
    var pythonExpression = ParseOk(pythonParser, sample, behavior);
    var lispText = lispParser.Decompile(pythonExpression);
    var lispExpression = ParseOk(lispParser, lispText, behavior);
    ValidateExpression(lispExpression, describer, sample);
    Assert.Equal(lispText, lispParser.Decompile(lispExpression));

    var pythonText = pythonParser.Decompile(lispExpression);
    var reparsedPythonExpression = ParseOk(pythonParser, pythonText, behavior);
    ValidateExpression(reparsedPythonExpression, describer, sample);
    Assert.Equal(pythonText, pythonParser.Decompile(reparsedPythonExpression));
  }

  static void AssertSamples(IEnumerable<string> samples, Action<string> validate) {
    var failures = new List<string>();
    foreach (var sample in samples) {
      try {
        validate(sample);
      } catch (Exception e) {
        failures.Add($"{sample}: {e.Message}");
      }
    }
    Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
  }

  static void ValidateExpression(IExpression expression, ExpressionDescriber describer, string sample) {
    Assert.True(!string.IsNullOrEmpty(describer.DescribeExpression(expression)), "Empty description for: " + sample);
    if (expression is BooleanOperator booleanOperator) {
      Assert.True(booleanOperator.Execute(), "Boolean expression evaluated to false: " + sample);
    }
  }

  static IExpression ParseOk(ParserBase parser, string input, AutomationBehavior behavior) {
    var result = parser.Parse(input, behavior);
    if (result.LastScriptError != null) {
      throw new InvalidOperationException($"Parse failed for '{input}': {result.LastError}");
    }
    return result.ParsedExpression;
  }

  static AutomationBehavior CreateBehavior() {
    var signals = new TestScriptable("Signals");
    signals.RegisterSignal("Signals.Var1", ScriptValue.TypeEnum.Number);
    signals.RegisterAction("Signals.Set", ScriptValue.TypeEnum.String, ScriptValue.TypeEnum.Number);

    var foobar = new TestScriptable("Foobar");
    foobar.RegisterAction("Foobar.EmptyAction");
    foobar.RegisterAction("Foobar.OneArgumentAction", ScriptValue.TypeEnum.Number);

    var debug = new TestScriptable("Debug");
    debug.RegisterVariadicAction("Debug.Log", ScriptValue.TypeEnum.String);

    TestScripting.CreateService(signals, foobar, debug);
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Foobar());
    return behavior;
  }

  sealed class Foobar : BaseComponent {
    public string str => "test";
    public int numInt => 123;
    public float numFloat => 123.33f;
    public bool boolFalse => false;
    public bool boolTrue => true;
    public List<string> strList => ["one", "two"];
    public List<int> numList => [1, 2];
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      var text = key switch {
          "IgorZ.Automation.Scripting.Expressions.AndOperator" => "AND",
          "IgorZ.Automation.Scripting.Expressions.OrOperator" => "OR",
          "IgorZ.Automation.Scripting.Expressions.NotOperator" => "NOT",
          _ => key,
      };
      return args.Length == 0 ? text : string.Format(text, args);
    }
  }
}
