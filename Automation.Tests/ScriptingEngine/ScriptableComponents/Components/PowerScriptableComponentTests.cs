using System;
using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.TimberDev.Utils;
using Timberborn.MechanicalSystem;

namespace Automation.Tests;

static class PowerScriptableComponentTests {
  public static void ExposesSignalsForMechanicalNode() {
    var harness = new Harness();

    var signalNames = harness.Component.GetSignalNamesForBuilding(harness.Behavior);

    Assert.Equal("Power", harness.Component.Name);
    Assert.Equal("Power.Supply", signalNames[0]);
    Assert.Equal("Power.Demand", signalNames[1]);
    Assert.Equal("Power.BatteryCharge", signalNames[2]);
    Assert.Equal("Power.BatteryCapacity", signalNames[3]);
    Assert.Equal("Power.BatteryChargeLevel", signalNames[4]);
  }

  public static void HidesSignalsForMissingMechanicalNode() {
    var harness = new Harness(withMechanicalNode: false);

    Assert.Equal(0, harness.Component.GetSignalNamesForBuilding(harness.Behavior).Length);
  }

  public static void ReadsCurrentGraphSignals() {
    var harness = new Harness();
    harness.MechanicalNode.Graph = new MechanicalGraph {
        PowerSupply = 120,
        PowerDemand = 80,
        BatteryCharge = 30,
        BatteryCapacity = 120,
    };

    Assert.Equal(120, harness.Component.GetSignalSource("Power.Supply", harness.Behavior)().AsInt);
    Assert.Equal(80, harness.Component.GetSignalSource("Power.Demand", harness.Behavior)().AsInt);
    Assert.Equal(30, harness.Component.GetSignalSource("Power.BatteryCharge", harness.Behavior)().AsInt);
    Assert.Equal(120, harness.Component.GetSignalSource("Power.BatteryCapacity", harness.Behavior)().AsInt);
    Assert.Equal(25, harness.Component.GetSignalSource("Power.BatteryChargeLevel", harness.Behavior)().AsRawNumber);
  }

  public static void ReadsZeroesWithoutGraph() {
    var harness = new Harness();
    harness.MechanicalNode.Graph = null;

    Assert.Equal(0, harness.Component.GetSignalSource("Power.Supply", harness.Behavior)().AsInt);
    Assert.Equal(0, harness.Component.GetSignalSource("Power.Demand", harness.Behavior)().AsInt);
    Assert.Equal(0, harness.Component.GetSignalSource("Power.BatteryCharge", harness.Behavior)().AsInt);
    Assert.Equal(0, harness.Component.GetSignalSource("Power.BatteryCapacity", harness.Behavior)().AsInt);
    Assert.Equal(0, harness.Component.GetSignalSource("Power.BatteryChargeLevel", harness.Behavior)().AsRawNumber);
  }

  public static void BuildsSignalDefinitions() {
    var harness = new Harness();

    var supplyDef = harness.Component.GetSignalDefinition("Power.Supply", harness.Behavior);
    var levelDef = harness.Component.GetSignalDefinition("Power.BatteryChargeLevel", harness.Behavior);

    Assert.Equal("Power.Supply", supplyDef.ScriptName);
    Assert.Equal(SignalDef.ScopeEnum.Building, supplyDef.Scope);
    Assert.Equal(ScriptValue.TypeEnum.Number, supplyDef.Result.ValueType);
    Assert.Equal(ValueDef.NumericFormatEnum.Integer, supplyDef.Result.DisplayNumericFormat);
    Assert.Equal("Power.BatteryChargeLevel", levelDef.ScriptName);
    Assert.Equal(SignalDef.ScopeEnum.Building, levelDef.Scope);
    Assert.Equal(ValueDef.NumericFormatEnum.Percent, levelDef.Result.DisplayNumericFormat);
    Assert.Equal((0, 100), levelDef.Result.DisplayNumericFormatRange);
  }

  public static void TicksOnlyWithListenersAndNotifiesOnChange() {
    var harness = new Harness();
    var listener = new TestSignalListener(harness.Behavior);
    var signal = Signal("Power.Supply", harness.Behavior);

    harness.Component.RegisterSignalChangeCallback(signal, listener);
    harness.AutomationService.Tick(1);

    Assert.Equal(1, harness.AutomationService.RegisteredTickables.Count);
    Assert.Equal(0, listener.Calls);

    harness.MechanicalNode.Graph.PowerSupply = 20;
    harness.AutomationService.Tick(2);
    harness.AutomationService.Tick(3);

    Assert.Equal(1, listener.Calls);
    Assert.Equal("Power.Supply", listener.LastSignalName);

    harness.Component.UnregisterSignalChangeCallback(signal, listener);
    harness.MechanicalNode.Graph.PowerSupply = 30;
    harness.AutomationService.Tick(4);

    Assert.Equal(0, harness.AutomationService.RegisteredTickables.Count);
    Assert.Equal(1, listener.Calls);
  }

  public static void TickReadsNewGraphAfterNetworkChanges() {
    var harness = new Harness();
    var listener = new TestSignalListener(harness.Behavior);
    var signal = Signal("Power.BatteryCharge", harness.Behavior);

    harness.Component.RegisterSignalChangeCallback(signal, listener);
    harness.MechanicalNode.Graph = new MechanicalGraph {
        BatteryCharge = 10,
        BatteryCapacity = 100,
    };
    harness.AutomationService.Tick(1);
    harness.MechanicalNode.Graph = new MechanicalGraph {
        BatteryCharge = 75,
        BatteryCapacity = 100,
    };
    harness.AutomationService.Tick(2);

    Assert.Equal(2, listener.Calls);
    Assert.Equal("Power.BatteryCharge", listener.LastSignalName);
    Assert.Equal(75, harness.Component.GetSignalSource("Power.BatteryCharge", harness.Behavior)().AsInt);
  }

  public static void ReportsUnknownSignal() {
    var harness = new Harness();

    Assert.Throws<ScriptError.ParsingError>(() => harness.Component.GetSignalSource("Power.Missing", harness.Behavior));
    Assert.Throws<ScriptError.ParsingError>(() => harness.Component.GetSignalDefinition("Power.Missing", harness.Behavior));
  }

  static SignalOperator Signal(string signalName, AutomationBehavior behavior) {
    return SignalOperator.Create(new ExpressionContext { ScriptHost = behavior }, signalName);
  }

  static void SetDependencyContainer(IContainer container) {
    var constructor = typeof(StaticBindings).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IContainer)],
        null);
    constructor.Invoke([container]);
  }

  sealed class Harness {
    public readonly AutomationBehavior Behavior = new();
    public readonly AutomationService AutomationService = new();
    public readonly ScriptingService ScriptingService;
    public readonly PowerScriptableComponent Component;
    public readonly MechanicalNode MechanicalNode;

    public Harness(bool withMechanicalNode = true) {
      ScriptingService = TestScripting.CreateService();
      Component = CreateComponent(AutomationService);
      Component.InjectDependencies(AutomationService.Loc, ScriptingService);
      Component.Load();
      if (withMechanicalNode) {
        MechanicalNode = new MechanicalNode {
            Graph = new MechanicalGraph(),
        };
        Behavior.SetComponent(MechanicalNode);
      }
      SetDependencyContainer(new TestContainer());
      Behavior.InjectDependencies(AutomationService);
      Behavior.Awake();
    }

    static PowerScriptableComponent CreateComponent(AutomationService automationService) {
      var constructor = typeof(PowerScriptableComponent).GetConstructor(
          BindingFlags.Instance | BindingFlags.NonPublic,
          null,
          [typeof(AutomationService)],
          null);
      return (PowerScriptableComponent)constructor.Invoke([automationService]);
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
