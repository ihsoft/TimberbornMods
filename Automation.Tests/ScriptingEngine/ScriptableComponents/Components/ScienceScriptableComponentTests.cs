using System;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.ScienceSystem;

namespace Automation.Tests;

static class ScienceScriptableComponentTests {
  public static void ExposesPointsSignalAndDefinition() {
    var harness = new Harness();

    var signalNames = harness.Component.GetSignalNamesForBuilding(harness.Behavior);
    var signalDef = harness.Component.GetSignalDefinition("Science.Points", harness.Behavior);

    Assert.Equal("Science", harness.Component.Name);
    Assert.Equal("Science.Points", signalNames[0]);
    Assert.Equal("Science.Points", signalDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.Number, signalDef.Result.ValueType);
    Assert.Equal(ValueDef.NumericFormatEnum.Integer, signalDef.Result.DisplayNumericFormat);
  }

  public static void ReadsCurrentSciencePoints() {
    var harness = new Harness();

    harness.ScienceService.AddPoints(42);

    Assert.Equal(4200, harness.Component.GetSignalSource("Science.Points", harness.Behavior)().AsRawNumber);
  }

  public static void TicksOnlyWithListenersAndNotifiesOnChange() {
    var harness = new Harness();
    var listener = new TestSignalListener(harness.Behavior);
    var signal = Signal("Science.Points", harness.Behavior);

    harness.Component.RegisterSignalChangeCallback(signal, listener);
    harness.AutomationService.Tick(1);

    Assert.Equal(1, harness.AutomationService.RegisteredTickables.Count);
    Assert.Equal(0, listener.Calls);

    harness.ScienceService.AddPoints(10);
    harness.AutomationService.Tick(2);
    harness.AutomationService.Tick(3);

    Assert.Equal(1, listener.Calls);
    Assert.Equal("Science.Points", listener.LastSignalName);

    harness.Component.UnregisterSignalChangeCallback(signal, listener);
    harness.ScienceService.AddPoints(10);
    harness.AutomationService.Tick(4);

    Assert.Equal(0, harness.AutomationService.RegisteredTickables.Count);
    Assert.Equal(1, listener.Calls);
  }

  public static void ReportsUnknownSignal() {
    var harness = new Harness();

    Assert.Throws<ScriptError.ParsingError>(
        () => harness.Component.GetSignalDefinition("Science.Missing", harness.Behavior));
    Assert.Throws<ScriptError.ParsingError>(
        () => harness.Component.RegisterSignalChangeCallback(Signal("Science.Missing", harness.Behavior), null));
  }

  static SignalOperator Signal(string signalName, AutomationBehavior behavior) {
    return SignalOperator.Create(new ExpressionContext { ScriptHost = behavior }, signalName);
  }

  sealed class Harness {
    public readonly AutomationBehavior Behavior = new();
    public readonly AutomationService AutomationService = new();
    public readonly ScienceService ScienceService = new();
    public readonly ScriptingService ScriptingService;
    public readonly ScienceScriptableComponent Component;

    public Harness() {
      ScriptingService = TestScripting.CreateService();
      Component = CreateComponent(AutomationService, ScienceService, new ReferenceManager(ScriptingService));
      Component.InjectDependencies(AutomationService.Loc, ScriptingService);
      Component.Load();
    }

    static ScienceScriptableComponent CreateComponent(
        AutomationService automationService, ScienceService scienceService, ReferenceManager referenceManager) {
      var constructor = typeof(ScienceScriptableComponent).GetConstructor(
          BindingFlags.Instance | BindingFlags.NonPublic,
          null,
          [typeof(AutomationService), typeof(ScienceService), typeof(ReferenceManager)],
          null);
      return (ScienceScriptableComponent)constructor.Invoke([automationService, scienceService, referenceManager]);
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
