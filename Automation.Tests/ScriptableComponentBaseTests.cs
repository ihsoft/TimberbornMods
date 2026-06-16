using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;

namespace Automation.Tests;

static class ScriptableComponentBaseTests {
  public static void ReturnsEmptyDefinitionsByDefault() {
    var component = CreateHarness(out _);
    var behavior = new AutomationBehavior();

    Assert.Equal(0, component.GetSignalNamesForBuilding(behavior).Length);
    Assert.Equal(0, component.GetActionNamesForBuilding(behavior).Length);
  }

  public static void ReportsUnknownSignalsAndActions() {
    var component = CreateHarness(out _);
    var behavior = new AutomationBehavior();
    var signal = CreateSignal();
    var action = CreateAction();

    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalSource("Signals.Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalDefinition("Signals.Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionExecutor("Signals.Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Signals.Missing", behavior));
    Assert.Throws<InvalidOperationException>(() => component.RegisterSignalChangeCallback(signal, null));
    Assert.Throws<InvalidOperationException>(() => component.UnregisterSignalChangeCallback(signal, null));

    component.InstallAction(action, behavior);
    component.UninstallAction(action, behavior);
  }

  public static void LoadRegistersScriptable() {
    var component = CreateHarness(out var service);

    component.Load();

    Assert.Equal("Harness", service.GetScriptableNames()[0]);
  }

  public static void FormatsArgumentHints() {
    var component = CreateHarness(out _);

    Assert.Equal("(1..5)", component.ArgumentMinMaxValueHint(1, 5));
    Assert.Equal(null, component.ArgumentMinMaxValueHint(1, int.MaxValue));
    Assert.Equal("IgorZ.Automation.Scripting.Editor.ArgumentMaxValueHint:5", component.ArgumentMaxValueHint(5));
    Assert.Equal("IgorZ.Automation.Scripting.Editor.ArgumentMaxValueHint:1.50", component.ArgumentMaxValueHint(1.5f));
    Assert.Equal(null, component.ArgumentMaxValueHint(-1.0f));
  }

  public static void GetsComponentOrReportsBadState() {
    var component = CreateHarness(out _);
    var behavior = new AutomationBehavior();
    var expected = new TestComponent();
    behavior.SetComponent(expected);

    Assert.Same(expected, component.ComponentOrThrow<TestComponent>(behavior));
    Assert.Throws<ScriptError.BadStateError>(() => component.ComponentOrThrow<MissingComponent>(behavior));
  }

  static Harness CreateHarness(out ScriptingService service) {
    service = TestScripting.CreateService();
    var component = new Harness();
    component.InjectDependencies(new TestLoc(), service);
    return component;
  }

  static SignalOperator CreateSignal() {
    var signals = new TestScriptable("Signals");
    signals.RegisterSignal("Signals.Custom", ScriptValue.TypeEnum.Number);
    TestScripting.CreateService(signals);
    return SignalOperator.Create(new ExpressionContext { ScriptHost = new AutomationBehavior() }, "Signals.Custom");
  }

  static ActionOperator CreateAction() {
    var signals = new TestScriptable("Signals");
    signals.RegisterAction("Signals.Do");
    TestScripting.CreateService(signals);
    return ActionOperator.Create(new ExpressionContext { ScriptHost = new AutomationBehavior() }, "Signals.Do", []);
  }

  sealed class Harness : ScriptableComponentBase {
    public override string Name => "Harness";

    public string ArgumentMinMaxValueHint(int minValue, int maxValue) {
      return GetArgumentMinMaxValueHint(minValue, maxValue);
    }

    public string ArgumentMaxValueHint(int maxValue) {
      return GetArgumentMaxValueHint(maxValue);
    }

    public string ArgumentMaxValueHint(float maxValue) {
      return GetArgumentMaxValueHint(maxValue);
    }

    public T ComponentOrThrow<T>(AutomationBehavior behavior) where T : BaseComponent {
      return GetComponentOrThrow<T>(behavior);
    }
  }

  sealed class TestComponent : BaseComponent {
  }

  sealed class MissingComponent : BaseComponent {
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key + ":" + args[0];
    }
  }
}
