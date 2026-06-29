using System;
using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.TimberDev.Utils;
using Timberborn.Localization;
using Timberborn.WaterBuildings;

namespace Automation.Tests;

static class StreamGaugeScriptableComponentTests {
  public static void ExposesSignalsForStreamGauge() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new StreamGauge());

    var signalNames = component.GetSignalNamesForBuilding(behavior);

    Assert.Equal("StreamGauge.Depth", signalNames[0]);
    Assert.Equal("StreamGauge.Contamination", signalNames[1]);
    Assert.Equal("StreamGauge.Current", signalNames[2]);
  }

  public static void HidesSignalsForMissingStreamGauge() {
    var component = CreateComponent();

    Assert.Equal(0, component.GetSignalNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ReadsStreamGaugeSignals() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new StreamGauge {
        WaterLevel = 1.25f,
        ContaminationLevel = 0.75f,
        WaterCurrent = 2.5f,
    });

    Assert.Equal(125, component.GetSignalSource("StreamGauge.Depth", behavior)().AsRawNumber);
    Assert.Equal(75, component.GetSignalSource("StreamGauge.Contamination", behavior)().AsRawNumber);
    Assert.Equal(250, component.GetSignalSource("StreamGauge.Current", behavior)().AsRawNumber);
  }

  public static void BuildsSignalDefinitions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new StreamGauge());

    Assert.Equal(ValueDef.NumericFormatEnum.Float,
        component.GetSignalDefinition("StreamGauge.Depth", behavior).Result.DisplayNumericFormat);
    Assert.Equal(ValueDef.NumericFormatEnum.Percent,
        component.GetSignalDefinition("StreamGauge.Contamination", behavior).Result.DisplayNumericFormat);
    Assert.Equal(ValueDef.NumericFormatEnum.Float,
        component.GetSignalDefinition("StreamGauge.Current", behavior).Result.DisplayNumericFormat);
  }

  public static void ReportsUnknownSignal() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new StreamGauge());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalDefinition("StreamGauge.Missing", behavior));
  }

  public static void TickerAwakeDoesNotCreateTracker() {
    SetDependencyContainer(new ActivatorContainer());
    var behavior = new AutomationBehavior();
    var ticker = new StreamGaugeScriptableComponent.StreamGaugeCheckTicker();
    behavior.SetComponent(new StreamGauge());
    behavior.SetComponent(ticker);
    ticker.SetComponent(behavior);

    ticker.Awake();

    Assert.False(ticker.Enabled);
    Assert.False(behavior.TryGetDynamicComponent<StreamGaugeScriptableComponent.StreamGaugeTracker>(out _));
  }

  public static void RegisterSignalCreatesTrackerAndEnablesTicker() {
    SetDependencyContainer(new ActivatorContainer());
    var component = CreateComponent();
    ScriptingService.Instance.RegisterScriptable(component);
    var behavior = new AutomationBehavior();
    var ticker = new StreamGaugeScriptableComponent.StreamGaugeCheckTicker();
    behavior.SetComponent(new StreamGauge());
    behavior.SetComponent(ticker);
    ticker.SetComponent(behavior);
    ticker.Awake();

    component.RegisterSignalChangeCallback(Signal("StreamGauge.Depth", behavior), new TestSignalListener(behavior));

    Assert.True(behavior.TryGetDynamicComponent<StreamGaugeScriptableComponent.StreamGaugeTracker>(out var tracker));
    Assert.True(ticker.Enabled);
    Assert.True(tracker != null);
  }

  static SignalOperator Signal(string signalName, AutomationBehavior behavior) {
    return SignalOperator.Create(new ExpressionContext { ScriptHost = behavior }, signalName);
  }

  static StreamGaugeScriptableComponent CreateComponent() {
    var component = new StreamGaugeScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }

  static void SetDependencyContainer(IContainer container) {
    var constructor = typeof(StaticBindings).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IContainer)],
        null);
    constructor.Invoke([container]);
  }

  sealed class ActivatorContainer : IContainer {
    public object GetInstance(Type type) {
      return Activator.CreateInstance(type);
    }

    public T GetInstance<T>() {
      return (T)GetInstance(typeof(T));
    }
  }

  sealed class TestSignalListener(AutomationBehavior behavior) : ISignalListener {
    public AutomationBehavior Behavior { get; } = behavior;

    public void OnValueChanged(string signalName) {
    }
  }
}
