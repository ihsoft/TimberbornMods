using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;

namespace Automation.Tests;

static class ParserTests {
  public static void PythonParserPreservesPrecedence() {
    var parser = new PythonSyntaxParser();

    var expression = ParseOk(parser, "1 + 2 * 3 == 7 and not 4 < 3");

    Assert.True(expression is LogicalOperator { OperatorType: LogicalOperator.OpType.And });
    Assert.True(((BooleanOperator)expression).Execute());
    AssertRoundTrip(parser, expression, "1 + 2 * 3 == 7 and not 4 < 3");
  }

  public static void PythonParserHandlesStringsAndConcat() {
    var parser = new PythonSyntaxParser();

    var expression = ParseOk(parser, "concat('one', \"-\", 2 + 3) == 'one-5'");

    Assert.True(((BooleanOperator)expression).Execute());
    AssertRoundTrip(parser, expression, "concat('one', '-', 2 + 3) == 'one-5'");
  }

  public static void PythonParserParsesSignalsAndActions() {
    var parser = new PythonSyntaxParser();
    ResetScriptingService();
    ScriptingService.Instance.RegisterSignal(
        "Signals.Var1",
        ScriptValue.TypeEnum.Number,
        () => ScriptValue.FromInt(5));
    ScriptingService.Instance.RegisterAction("Signals.Set", ScriptValue.TypeEnum.String, ScriptValue.TypeEnum.Number);

    var signalExpression = ParseOk(parser, "Signals.Var1 >= 5");
    var actionExpression = ParseOk(parser, "Signals.Set('yellow', Signals.Var1 + 123)");

    Assert.True(signalExpression is ComparisonOperator { OperatorType: ComparisonOperator.OpType.GreaterThanOrEqual });
    Assert.True(((BooleanOperator)signalExpression).Execute());
    Assert.True(actionExpression is ActionOperator { ActionName: "Signals.Set" });
    AssertRoundTrip(parser, signalExpression, "Signals.Var1 >= 5");
    AssertRoundTrip(parser, actionExpression, "Signals.Set('yellow', Signals.Var1 + 123)");
  }

  public static void LispParserRoundTripsExpressions() {
    var parser = new LispSyntaxParser();

    var expression = ParseOk(parser, "(and (eq (add 100 (mul 200 300)) 700) (not (lt 400 300)))");

    Assert.True(expression is LogicalOperator { OperatorType: LogicalOperator.OpType.And });
    Assert.True(((BooleanOperator)expression).Execute());
    AssertRoundTrip(parser, expression, "(and (eq (add 100 (mul 200 300)) 700) (not (lt 400 300)))");
  }

  public static void ParsersRoundTripAcrossSyntaxes() {
    var pythonParser = new PythonSyntaxParser();
    var lispParser = new LispSyntaxParser();

    var pythonExpression = ParseOk(pythonParser, "1 + 2 + (3 - 4) == 2");
    var lispText = lispParser.Decompile(pythonExpression);
    var lispExpression = ParseOk(lispParser, lispText);
    var pythonText = pythonParser.Decompile(lispExpression);
    var reparsedPythonExpression = ParseOk(pythonParser, pythonText);

    Assert.Equal("(eq (add 100 200 (sub 300 400)) 200)", lispText);
    Assert.Equal("1 + 2 + 3 - 4 == 2", pythonText);
    Assert.True(((BooleanOperator)reparsedPythonExpression).Execute());
  }

  public static void PythonParserRejectsMalformedExpressions() {
    var parser = new PythonSyntaxParser();

    AssertParseFails(parser, "1 + ()");
    AssertParseFails(parser, "1 + (1))");
    AssertParseFails(parser, "'test' > 'test'");
    AssertParseFails(parser, "Signals.1Var");
    AssertParseFails(parser, "1.234 == 1.23");
  }

  public static void LispParserRejectsMalformedExpressions() {
    var parser = new LispSyntaxParser();

    AssertParseFails(parser, "(add 1)");
    AssertParseFails(parser, "(eq 'test' 123)");
    AssertParseFails(parser, "(unknown 1 2)");
    AssertParseFails(parser, "(add 1 2");
  }

  static IExpression ParseOk(ParserBase parser, string input) {
    var result = parser.Parse(input, new AutomationBehavior());
    if (result.LastScriptError != null) {
      throw new InvalidOperationException($"Parse failed for '{input}': {result.LastError}");
    }
    return result.ParsedExpression;
  }

  static void AssertParseFails(ParserBase parser, string input) {
    var result = parser.Parse(input, new AutomationBehavior());
    Assert.True(result.LastScriptError != null, "Expected parse to fail: " + input);
    Assert.Equal(null, result.ParsedExpression);
  }

  static void AssertRoundTrip(ParserBase parser, IExpression expression, string expected) {
    var decompiled = parser.Decompile(expression);
    var reparsed = ParseOk(parser, decompiled);
    Assert.Equal(expected, decompiled);
    Assert.Equal(decompiled, parser.Decompile(reparsed));
  }

  static void ResetScriptingService() {
    ScriptingService.Instance.Reset();
  }
}
