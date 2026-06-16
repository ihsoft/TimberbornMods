using System;
using System.Collections.Generic;

namespace Automation.Tests;

static class Program {
  static readonly List<(string Name, Action Test)> Tests = [
      ("AutomationBehavior creates dynamic component only once", AutomationBehaviorTests.GetOrCreateCachesComponent),
      ("AutomationBehavior reports missing dynamic component", AutomationBehaviorTests.GetOrThrowReportsMissingComponent),
      ("AutomationBehavior creates component with Awake callback", AutomationBehaviorTests.GetOrCreateCallsAwake),
      ("AutomationBehavior replays finished callback after late creation", AutomationBehaviorTests.GetOrCreateAfterFinished),
      ("AutomationBehavior replays initialized callback after late creation", AutomationBehaviorTests.GetOrCreateAfterInitialized),
      ("AutomationBehavior replays finished and initialized callbacks in order",
          AutomationBehaviorTests.GetOrCreateAfterFinishedAndInitialized),
      ("AutomationBehavior forwards lifecycle callbacks to existing dynamic components",
          AutomationBehaviorTests.ForwardsLifecycleCallbacks),
      ("AutomationBehavior delete forwards to dynamic components", AutomationBehaviorTests.DeleteEntityForwardsToComponents),
      ("Python parser preserves math and logical precedence", ParserTests.PythonParserPreservesPrecedence),
      ("Python parser handles strings and concat", ParserTests.PythonParserHandlesStringsAndConcat),
      ("Python parser parses signal comparisons and actions", ParserTests.PythonParserParsesSignalsAndActions),
      ("Lisp parser round-trips comparison and math expressions", ParserTests.LispParserRoundTripsExpressions),
      ("Parsers can round-trip between Python and Lisp", ParserTests.ParsersRoundTripAcrossSyntaxes),
      ("Python parser rejects malformed expressions", ParserTests.PythonParserRejectsMalformedExpressions),
      ("Lisp parser rejects malformed expressions", ParserTests.LispParserRejectsMalformedExpressions),
      ("ScriptValue stores fixed precision numbers", ExpressionTests.ScriptValueStoresFixedPrecisionNumbers),
      ("ScriptValue arithmetic uses raw fixed precision", ExpressionTests.ScriptValueArithmeticUsesRawFixedPrecision),
      ("ScriptValue rejects invalid value access", ExpressionTests.ScriptValueRejectsInvalidAccess),
      ("MathOperator executes numeric functions", ExpressionTests.MathOperatorExecutesNumericFunctions),
      ("ComparisonOperator executes number and string comparisons", ExpressionTests.ComparisonOperatorExecutesComparisons),
      ("LogicalOperator executes boolean composition", ExpressionTests.LogicalOperatorExecutesBooleanComposition),
      ("ConcatOperator concatenates number and string values", ExpressionTests.ConcatOperatorConcatenatesValues),
      ("Operators reject invalid operands", ExpressionTests.OperatorsRejectInvalidOperands),
  ];

  static int Main() {
    var failed = 0;
    foreach (var (name, test) in Tests) {
      try {
        test();
        Console.WriteLine("[PASS] " + name);
      } catch (Exception e) {
        failed++;
        Console.WriteLine("[FAIL] " + name);
        Console.WriteLine(e);
      }
    }

    Console.WriteLine();
    Console.WriteLine($"Total: {Tests.Count}, Passed: {Tests.Count - failed}, Failed: {failed}");
    return failed == 0 ? 0 : 1;
  }
}
