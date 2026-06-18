using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.Automation.ScriptingEngineUI;
using Timberborn.WaterBuildings;

namespace Automation.Tests;

static class InvertRuleButtonProviderTests {
  public static void InvertsFillValveSetHeightArgumentWithoutChangingAction() {
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new FillValve { MinTargetHeight = 2, MaxTargetHeight = 5 });
    var parserFactory = CreateParserFactory();
    RegisterFillValveScriptableComponent();

    var zeroAction = ParseAction(parserFactory, behavior, "FillValve.SetHeight(0)");
    var maxAction = ParseAction(parserFactory, behavior, "FillValve.SetHeight(3)");

    Assert.Equal("(act FillValve.SetHeight 300)", MakeInvertedActionExpression(parserFactory, zeroAction, behavior));
    Assert.Equal("(act FillValve.SetHeight 0)", MakeInvertedActionExpression(parserFactory, maxAction, behavior));
  }

  public static void InvertsFillValveOpenAndCloseActions() {
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new FillValve { MinTargetHeight = 2, MaxTargetHeight = 5 });
    var parserFactory = CreateParserFactory();
    RegisterFillValveScriptableComponent();

    var openAction = ParseAction(parserFactory, behavior, "FillValve.Open()");
    var closeAction = ParseAction(parserFactory, behavior, "FillValve.Close()");

    Assert.Equal("(act FillValve.Close)", MakeInvertedActionExpression(parserFactory, openAction, behavior));
    Assert.Equal("(act FillValve.Open)", MakeInvertedActionExpression(parserFactory, closeAction, behavior));
  }

  static string MakeInvertedActionExpression(
      ParserFactory parserFactory, ActionOperator action, AutomationBehavior behavior) {
    var provider = new InvertRuleButtonProvider(parserFactory);
    var method = typeof(InvertRuleButtonProvider).GetMethod(
        "MakeInvertedActionExpression",
        BindingFlags.Instance | BindingFlags.NonPublic);
    return (string)method.Invoke(provider, [action, behavior]);
  }

  static ActionOperator ParseAction(ParserFactory parserFactory, AutomationBehavior behavior, string expression) {
    var action = parserFactory.ParseAction(expression, behavior, out var result, parserFactory.PythonSyntaxParser);
    if (result.LastScriptError != null) {
      throw result.LastScriptError;
    }
    return action;
  }

  static ParserFactory CreateParserFactory() {
    var constructor = typeof(ParserFactory).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(LispSyntaxParser), typeof(PythonSyntaxParser)],
        null);
    return (ParserFactory)constructor.Invoke([new LispSyntaxParser(), new PythonSyntaxParser()]);
  }

  static void RegisterFillValveScriptableComponent() {
    var service = TestScripting.CreateService();
    var component = new FillValveScriptableComponent();
    component.InjectDependencies(new TestLoc(), service);
    component.Load();
  }

  sealed class TestLoc : Timberborn.Localization.ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
