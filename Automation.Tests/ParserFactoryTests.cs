using System;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;

namespace Automation.Tests;

static class ParserFactoryTests {
  public static void SelectsParserByExplicitPrefix() {
    var factory = CreateFactory();
    var behavior = new AutomationBehavior();

    var pythonResult = factory.ParseExpression("#PY 1 + 2 == 3", behavior, preferredParser: factory.LispSyntaxParser);
    var lispResult = factory.ParseExpression("#LP (eq (add 100 200) 300)", behavior,
        preferredParser: factory.PythonSyntaxParser);

    Assert.Equal(null, pythonResult.LastScriptError);
    Assert.Equal(null, lispResult.LastScriptError);
    Assert.True(pythonResult.ParsedExpression is ComparisonOperator);
    Assert.True(lispResult.ParsedExpression is ComparisonOperator);
    Assert.True(((BooleanOperator)pythonResult.ParsedExpression).Execute());
    Assert.True(((BooleanOperator)lispResult.ParsedExpression).Execute());
  }

  public static void UsesPreferredParser() {
    var factory = CreateFactory();
    var behavior = new AutomationBehavior();

    var pythonResult = factory.ParseExpression("1 + 2 == 3", behavior, preferredParser: factory.PythonSyntaxParser);
    var lispResult = factory.ParseExpression("(eq (add 100 200) 300)", behavior,
        preferredParser: factory.LispSyntaxParser);

    Assert.Equal(null, pythonResult.LastScriptError);
    Assert.Equal(null, lispResult.LastScriptError);
    Assert.True(((BooleanOperator)pythonResult.ParsedExpression).Execute());
    Assert.True(((BooleanOperator)lispResult.ParsedExpression).Execute());
  }

  public static void ParsesValidConditionsAndActions() {
    var factory = CreateFactory();
    var behavior = new AutomationBehavior();
    ResetScriptingService();
    ScriptingService.Instance.RegisterSignal(
        "Signals.Var1",
        ScriptValue.TypeEnum.Number,
        () => ScriptValue.FromInt(5));
    ScriptingService.Instance.RegisterAction("Signals.Set", ScriptValue.TypeEnum.String, ScriptValue.TypeEnum.Number);

    var condition = factory.ParseCondition(
        "Signals.Var1 >= 5",
        behavior,
        out var conditionResult,
        preferredParser: factory.PythonSyntaxParser);
    var action = factory.ParseAction(
        "Signals.Set('yellow', 12)",
        behavior,
        out var actionResult,
        preferredParser: factory.PythonSyntaxParser);

    Assert.Equal(null, conditionResult.LastScriptError);
    Assert.Equal(null, actionResult.LastScriptError);
    Assert.True(condition.Execute());
    Assert.Equal("Signals.Set", action.ActionName);
  }

  public static void RejectsInvalidConditions() {
    var factory = CreateFactory();
    var behavior = new AutomationBehavior();

    var notBoolean = factory.ParseCondition(
        "1 + 2",
        behavior,
        out var notBooleanResult,
        preferredParser: factory.PythonSyntaxParser);
    var noSignals = factory.ParseCondition(
        "1 == 1",
        behavior,
        out var noSignalsResult,
        preferredParser: factory.PythonSyntaxParser);

    Assert.Equal(null, notBoolean);
    Assert.Equal(null, noSignals);
    AssertLocError("IgorZ.Automation.Scripting.Editor.ConditionMustBeBoolean", notBooleanResult);
    AssertLocError("IgorZ.Automation.Scripting.Editor.ConditionMustHaveSignals", noSignalsResult);
  }

  public static void RejectsNonActionExpressions() {
    var factory = CreateFactory();
    var behavior = new AutomationBehavior();

    var action = factory.ParseAction(
        "1 == 1",
        behavior,
        out var result,
        preferredParser: factory.PythonSyntaxParser);

    Assert.Equal(null, action);
    AssertLocError("IgorZ.Automation.Scripting.Editor.ActionMustBeAction", result);
  }

  static ParserFactory CreateFactory() {
    var constructor = typeof(ParserFactory).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(LispSyntaxParser), typeof(PythonSyntaxParser)],
        null);
    return (ParserFactory)constructor.Invoke([new LispSyntaxParser(), new PythonSyntaxParser()]);
  }

  static void AssertLocError(string expectedLocKey, ParsingResult result) {
    Assert.True(result.LastScriptError is ScriptError.LocParsingError);
    var locError = (ScriptError.LocParsingError)result.LastScriptError;
    Assert.Equal(expectedLocKey, locError.LocKey);
    Assert.Equal(null, result.ParsedExpression);
  }

  static void ResetScriptingService() {
    ScriptingService.Instance.Reset();
  }
}
