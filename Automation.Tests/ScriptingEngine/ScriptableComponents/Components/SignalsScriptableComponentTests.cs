using System;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;

namespace Automation.Tests;

static class SignalsScriptableComponentTests {
  public static void ExposesDefinitions() {
    var harness = new Harness();

    var signalDef = harness.Component.GetSignalDefinition("Signals.Custom", harness.Behavior);
    var actionDef = harness.Component.GetActionDefinition("Signals.Set", harness.Behavior);

    Assert.Equal("Signals", harness.Component.Name);
    Assert.Equal(0, harness.Component.GetSignalNamesForBuilding(harness.Behavior).Length);
    Assert.Equal("Signals.Custom", signalDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.Number, signalDef.Result.ValueType);
    Assert.Equal("Signals.Set", harness.Component.GetActionNamesForBuilding(harness.Behavior)[0]);
    Assert.Equal("Signals.Set", actionDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.String, actionDef.Arguments[0].ValueType);
    Assert.Equal(ScriptValue.TypeEnum.Number, actionDef.Arguments[1].ValueType);
  }

  public static void SetsManualSignalValues() {
    var harness = new Harness();
    var action = (ActionOperator)ParsePython("Signals.Set('Custom', 12)");

    harness.Component.InstallAction(action, harness.Behavior);
    harness.Component.GetActionExecutor("Signals.Set", harness.Behavior)(
        [ScriptValue.FromString("Custom"), ScriptValue.FromInt(12)]);

    Assert.Equal("Signals.Custom", harness.Component.GetSignalNamesForBuilding(harness.Behavior)[0]);
    Assert.Equal(1200, harness.Component.GetSignalSource("Signals.Custom", harness.Behavior)().AsRawNumber);

    harness.Component.UninstallAction(action, harness.Behavior);

    Assert.Equal(0, harness.Component.GetSignalNamesForBuilding(harness.Behavior).Length);
  }

  public static void ValidatesSignalNames() {
    var harness = new Harness();
    var actionDef = harness.Component.GetActionDefinition("Signals.Set", harness.Behavior);

    Assert.Throws<InvalidOperationException>(
        () => harness.Component.GetSignalDefinition("Debug.Custom", harness.Behavior));
    Assert.Throws<ScriptError.ParsingError>(
        () => harness.Component.GetSignalDefinition("Signals.1Bad", harness.Behavior));
    Assert.Throws<ScriptError.ParsingError>(
        () => actionDef.Arguments[0].ArgumentValidator(Number(1)));
    Assert.Throws<ScriptError.BadValue>(
        () => actionDef.Arguments[0].RuntimeValueValidator(ScriptValue.FromString("")));
    Assert.Throws<ScriptError.BadValue>(
        () => actionDef.Arguments[0].RuntimeValueValidator(ScriptValue.FromString("1Bad")));
  }

  static IExpression ParsePython(string expression) {
    var result = new PythonSyntaxParser().Parse(expression, new AutomationBehavior());
    if (result.LastScriptError != null) {
      throw new InvalidOperationException(result.LastError);
    }
    return result.ParsedExpression;
  }

  static ConstantValueExpr Number(int value) {
    return ConstantValueExpr.CreateFromValue(ScriptValue.FromInt(value));
  }

  sealed class Harness {
    public readonly AutomationBehavior Behavior = new();
    public readonly ScriptingService ScriptingService;
    public readonly SignalsScriptableComponent Component;

    public Harness() {
      ScriptingService = TestScripting.CreateService();
      var dispatcher = CreateSignalDispatcher(ScriptingService);
      Component = CreateComponent(new EmptySingletonLoader(), dispatcher);
      Component.InjectDependencies(new AutomationService().Loc, ScriptingService);
      Component.Load();
      Behavior.SetComponent(new Timberborn.EntitySystem.EntityComponent());
    }

    static SignalsScriptableComponent CreateComponent(
        ISingletonLoader singletonLoader, SignalDispatcher signalDispatcher) {
      var constructor = typeof(SignalsScriptableComponent).GetConstructor(
          BindingFlags.Instance | BindingFlags.NonPublic,
          null,
          [typeof(ISingletonLoader), typeof(SignalDispatcher)],
          null);
      return (SignalsScriptableComponent)constructor.Invoke([singletonLoader, signalDispatcher]);
    }

    static SignalDispatcher CreateSignalDispatcher(ScriptingService scriptingService) {
      var constructor = typeof(SignalDispatcher).GetConstructor(
          BindingFlags.Instance | BindingFlags.NonPublic,
          null,
          [typeof(ScriptingService), typeof(EventBus)],
          null);
      return (SignalDispatcher)constructor.Invoke([scriptingService, new EventBus()]);
    }
  }

  sealed class EmptySingletonLoader : ISingletonLoader {
    public bool TryGetSingleton(Timberborn.Persistence.SingletonKey singletonKey,
                                out Timberborn.Persistence.IObjectLoader objectLoader) {
      objectLoader = null;
      return false;
    }
  }
}
