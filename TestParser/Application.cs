using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.Automation.ScriptingEngineUI;
using TestParser.Stubs;
using TestParser.Stubs.Game;
using TestParser.Stubs.Patches;

namespace TestParser;

public class Application {
  public static void Main(string[] args) {
    var parser = new Application();
    parser.Run();
  }

  readonly Dictionary<string, string> _localizations = new();

  readonly List<string> _goodScriptSamples = [
      // String constants
      "\"te'st\" == 'te\\'st'",
      "'te\"st' == \"te\\\"st\"",
      @"'te\\st' == 'te\\st'",
      "'test' != 'test2'",

      // Number constants
      "-1. == -1",
      "12. == 12",
      "123.0 == 123",
      "-123.0 == -123",
      "123.-10 == 113",  // 123.0 - 10

      // Custom signals.
      "Signals.Set('yellow', 12)",
      "Signals.Set('yellow', Signals.Var1 + 123)",

      // Actions
      "Foobar.EmptyAction()",
      "Foobar.OneArgumentAction(1)",
      "Foobar.OneArgumentAction(Signals.Var1 + 123)",

      // Concat function.
      "concat(1, '-test-', 2) == '1-test-2'",
      "concat(Signals.Var1, '-test-', 1+2+3) == '0-test-6'",

      // Get property operator.
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

      // Math equations
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
      "1.001 == 1.00",  // FIXME: probably fail on such constants?
      "1.006 == 1.01",
      "1 + 0.006 == 1.01",
      "1.003 + 0.003 == 1",
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
      "round(1.333) == 1",
      "round(1.555) == 2",
      "round(1/3) == 0",
      "round(4/3) == 1",
      "round(2/3) == 1",
      "round(5/3) == 2",

      // Actions
      "Debug.Log('foo={0}, bar={1}', 1, 'test')",
  ];

  readonly List<string> _badScriptSamples = [
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
      "getnum('test')",  // Bad name format. Must be: foo.bar.
      "getstr('Foobar.numInt') == 'foo'",  // getstr auto detects value type.
      "getnum('Foobar.str') == 1",  // getnum auto detects value type.
      "getstr('Foobar.strList')",
      "getnum('foobar.numList', 2)",  // Index out of range.
      "getnum('foobar.numList', 1, 1)",
      "getnum(1)",
      "getnum()",
      "getstr()",
      "getnum(Signals.Var1)", // must be a string literal
      //FIXME: try non-value index or non-constant name.
      "concat()",
      "min(1)",
      "max(2)",
      "round()",
  ];

  IContainer _container;

  void Run() {
    const bool showErrorsOnly = true;
    RegisterComponents();
    PatchStubs.Apply();

    // TestOneStatement("1 + 2 + 3 + 4", out var reports);
    // Console.WriteLine(string.Join("\n", reports));

    RunGoodScriptSamples(_goodScriptSamples, showErrorsOnly);
    RunBadScriptSamples(_badScriptSamples, showErrorsOnly);
  }

  void RunGoodScriptSamples(IList<string> samples, bool showErrorsOnly = false) {
    Console.WriteLine("Samples that must pass:");
    var success = 0;
    foreach (var testFormula in samples) {
      var testPassed = TestOneStatement(testFormula, out var reports);
      if (testPassed) {
        success++;
      }
      if (!testPassed || !showErrorsOnly) {
        Console.WriteLine(string.Join("\n", reports));
      }
    }
    Console.WriteLine($"{success} of {samples.Count} test samples passed\n");
  }

  void RunBadScriptSamples(IList<string> samples, bool showErrorsOnly = false) {
    var pyParser = _container.GetInstance<PythonSyntaxParser>();
    var behavior = new AutomationBehavior();
    Console.WriteLine("Samples that must fail:");
    var success = 0;
    foreach (var testFormula in samples) {
      var result = pyParser.Parse(testFormula, behavior);
      if (result.LastScriptError == null) {
        Console.WriteLine("[FAIL] Pattern: " + testFormula);
        Console.WriteLine("  * Didn't fail: " + result.ParsedExpression);
        continue;
      }
      success++;
      if (!showErrorsOnly) {
        Console.WriteLine("[PASS] Pattern: " + testFormula);
      }
    }
    Console.WriteLine($"{success} of {samples.Count} test samples passed\n");
  }

  bool TestOneStatement(string input, out List<string> reports) {
    var res = true;
    reports = new List<string>();
    var pyParser = _container.GetInstance<PythonSyntaxParser>();
    var lispParser = _container.GetInstance<LispSyntaxParser>();
    var behavior = new AutomationBehavior();
    reports.Add($"Testing: {input}");
    var result = pyParser.Parse(input, behavior);
    if (result.ParsedExpression == null) {
      reports.Add($"  * ERROR: Failed to parse input as Python: {result.LastError}");
      return false;
    }

    var decompiled1 = lispParser.Decompile(result.ParsedExpression);
    reports.Add($"Decompiled Lisp: {decompiled1}");
    result = lispParser.Parse(decompiled1, behavior);
    if (result.LastScriptError != null) {
      reports.Add($"  * ERROR: Failed to parse decompiled Python: {result.LastError}");
      return false;
    }
    TryMakeDescription(result.ParsedExpression, reports);
    res &= TryBooleanOperator(result.ParsedExpression, reports);
    var decompiled2 = lispParser.Decompile(result.ParsedExpression);
    if (decompiled1 != decompiled2) {
      reports.Add($"  * ERROR: {decompiled1} is not {decompiled2}");
      res = false;
    }

    decompiled1 = pyParser.Decompile(result.ParsedExpression);
    reports.Add($"Decompiled Python: {decompiled1}");
    result = pyParser.Parse(decompiled1, behavior);
    if (result.LastScriptError != null) {
      reports.Add($"  * ERROR: Failed to parse decompiled Python: {result.LastError}");
      return false;
    }
    TryMakeDescription(result.ParsedExpression, reports);
    res &= TryBooleanOperator(result.ParsedExpression, reports);
    decompiled2 = pyParser.Decompile(result.ParsedExpression);
    if (decompiled1 != decompiled2) {
      reports.Add($"  * ERROR: {decompiled1} is not {decompiled2}");
    }

    return res;
  }

  void TryMakeDescription(IExpression expr, List<string> reports) {
    var describer = _container.GetInstance<ExpressionDescriber>();
    try {
      var description = describer.DescribeExpression(expr);
      reports.Add($"  * Description: {description}");
    } catch (Exception e) {
      reports.Add($"  * Failed making description: {e.Message}");
    }
  }

  bool TryBooleanOperator(IExpression expr, List<string> reports) {
    if (expr is not BooleanOperator boolOperator) {
      return true;
    }
    var res = true;
    try {
      if (!boolOperator.Execute()) {
        reports.Add($"  * ERROR: Boolean operator executed to FALSE");
        res = false;
      } else {
        reports.Add($"  * Boolean operator executed to TRUE");
      }
    } catch (ScriptError e) {
      reports.Add($"  * ERROR: Failed executing boolean operator: {e.Message}");
      res = false;
    }
    return res;
  }

  void RegisterComponents() {
    IConfigurator[] configurators = [
        new StubsConfigurator(),
        new IgorZ.Automation.ScriptingEngine.Parser.Configurator(),
        new ComponentsConfigurator(),
    ]; 
    _container = Bindito.Core.Bindito.CreateContainer(configurators);

    var scriptingService = _container.GetInstance<ScriptingService>();
    scriptingService.RegisterScriptable(_container.GetInstance<DebugScriptableComponent>());
    scriptingService.RegisterScriptable(_container.GetInstance<SignalsScriptableComponent>());
    scriptingService.RegisterScriptable(_container.GetInstance<FoobarScriptingComponent>());
  }

  class ComponentsConfigurator : IConfigurator {
    public void Configure(IContainerDefinition containerDefinition) {
      containerDefinition.Bind<SignalDispatcher>().AsTransient();
      containerDefinition.Bind<ReferenceManager>().AsTransient();

      containerDefinition.Bind<AutomationService>().AsSingleton();
      containerDefinition.Bind<ExpressionDescriber>().AsSingleton();
      containerDefinition.Bind<ScriptingService>().AsSingleton();
      containerDefinition.Bind<DebugScriptableComponent>().AsSingleton();
      containerDefinition.Bind<SignalsScriptableComponent>().AsSingleton();
      containerDefinition.Bind<FoobarScriptingComponent>().AsSingleton();
    }
  }
}
