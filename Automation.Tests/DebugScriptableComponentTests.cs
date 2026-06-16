using System;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

namespace Automation.Tests;

static class DebugScriptableComponentTests {
  public static void ExposesDefinitions() {
    var harness = new Harness();

    var signalDef = harness.Component.GetSignalDefinition("Debug.Ticker", harness.Behavior);
    var logStrDef = harness.Component.GetActionDefinition("Debug.LogStr", harness.Behavior);
    var logNumDef = harness.Component.GetActionDefinition("Debug.LogNum", harness.Behavior);
    var logDef = harness.Component.GetActionDefinition("Debug.Log", harness.Behavior);

    Assert.Equal("Debug", harness.Component.Name);
    Assert.Equal(0, harness.Component.GetSignalNamesForBuilding(harness.Behavior).Length);
    Assert.Equal("Debug.LogStr", harness.Component.GetActionNamesForBuilding(harness.Behavior)[0]);
    Assert.Equal("Debug.LogNum", harness.Component.GetActionNamesForBuilding(harness.Behavior)[1]);
    Assert.Equal("Debug.Ticker", signalDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.Number, signalDef.Result.ValueType);
    Assert.Equal("Debug.LogStr", logStrDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.String, logStrDef.Arguments[0].ValueType);
    Assert.Equal("Debug.LogNum", logNumDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.Number, logNumDef.Arguments[0].ValueType);
    Assert.Equal("Debug.Log", logDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.String, logDef.Arguments[0].ValueType);
    Assert.Equal(ScriptValue.TypeEnum.Unset, logDef.VarArg.ValueType);
  }

  public static void ExecutesLogActions() {
    var harness = new Harness();

    harness.Component.GetActionExecutor("Debug.LogStr", harness.Behavior)([ScriptValue.FromString("text")]);
    harness.Component.GetActionExecutor("Debug.LogNum", harness.Behavior)([ScriptValue.FromFloat(1.5f)]);
    harness.Component.GetActionExecutor("Debug.Log", harness.Behavior)(
        [
            ScriptValue.FromString("text={0}, num={1}"),
            ScriptValue.FromString("value"),
            ScriptValue.FromFloat(2.5f),
        ]);

    Assert.Throws<ScriptError.ParsingError>(
        () => harness.Component.GetActionExecutor("Debug.LogStr", harness.Behavior)([]));
    Assert.Throws<ScriptError.ParsingError>(
        () => harness.Component.GetActionExecutor("Debug.Log", harness.Behavior)([]));
    Assert.Throws<ScriptError.ParsingError>(
        () => harness.Component.GetActionDefinition("Debug.Missing", harness.Behavior));
  }

  public static void RegistersTickerCallbacks() {
    var harness = new Harness();
    var listener = new TestSignalListener(harness.Behavior);
    var ticker = (SignalOperator)ParsePython("Debug.Ticker");

    harness.Component.RegisterSignalChangeCallback(ticker, listener);
    harness.AutomationService.Tick(12);

    Assert.Equal(1, harness.AutomationService.RegisteredTickables.Count);
    Assert.Equal(1200, harness.Component.GetSignalSource("Debug.Ticker", harness.Behavior)().AsRawNumber);
    Assert.Equal(1, listener.Calls);
    Assert.Equal("Debug.Ticker", listener.LastSignalName);

    harness.Component.UnregisterSignalChangeCallback(ticker, listener);
    harness.AutomationService.Tick(13);

    Assert.Equal(0, harness.AutomationService.RegisteredTickables.Count);
    Assert.Equal(1, listener.Calls);
  }

  static IExpression ParsePython(string expression) {
    var result = new PythonSyntaxParser().Parse(expression, new AutomationBehavior());
    if (result.LastScriptError != null) {
      throw new InvalidOperationException(result.LastError);
    }
    return result.ParsedExpression;
  }

  sealed class Harness {
    public readonly AutomationBehavior Behavior = new();
    public readonly AutomationService AutomationService = new();
    public readonly ScriptingService ScriptingService;
    public readonly DebugScriptableComponent Component;

    public Harness() {
      ScriptingService = TestScripting.CreateService();
      var referenceManager = CreateReferenceManager(ScriptingService);
      Component = CreateComponent(AutomationService, referenceManager);
      Component.InjectDependencies(AutomationService.Loc, ScriptingService);
      Component.Load();
    }

    static DebugScriptableComponent CreateComponent(
        AutomationService automationService, ReferenceManager referenceManager) {
      var constructor = typeof(DebugScriptableComponent).GetConstructor(
          BindingFlags.Instance | BindingFlags.NonPublic,
          null,
          [typeof(AutomationService), typeof(ReferenceManager)],
          null);
      return (DebugScriptableComponent)constructor.Invoke([automationService, referenceManager]);
    }

    static ReferenceManager CreateReferenceManager(ScriptingService scriptingService) {
      var constructor = typeof(ReferenceManager).GetConstructor(
          BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
          null,
          [typeof(ScriptingService)],
          null);
      return (ReferenceManager)constructor.Invoke([scriptingService]);
    }
  }

  sealed class TestSignalListener(AutomationBehavior behavior) : ISignalListener {
    public AutomationBehavior Behavior { get; } = behavior;
    public int Calls { get; private set; }
    public string LastSignalName { get; private set; }

    public void OnValueChanged(string signalName) {
      Calls++;
      LastSignalName = signalName;
    }
  }
}
