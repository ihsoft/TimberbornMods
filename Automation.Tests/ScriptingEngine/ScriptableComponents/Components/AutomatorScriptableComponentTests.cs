using System;
using System.Linq;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Automation;
using Timberborn.EntitySystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;

namespace Automation.Tests;

static class AutomatorScriptableComponentTests {
  public static void ExposesSignalForTransmitterAutomator() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Automator { IsTransmitter = true });

    Assert.Equal("Automator.State", component.GetSignalNamesForBuilding(behavior)[0]);
  }

  public static void HidesSignalForMissingOrNonTransmitterAutomator() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Automator { IsTransmitter = false });

    Assert.Equal(0, component.GetSignalNamesForBuilding(behavior).Length);
    Assert.Equal(0, component.GetSignalNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ReadsAutomatorState() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var automator = new Automator { IsTransmitter = true, State = AutomatorState.On };
    behavior.SetComponent(automator);

    Assert.Equal(100, component.GetSignalSource("Automator.State", behavior)().AsRawNumber);

    automator.State = AutomatorState.Off;

    Assert.Equal(0, component.GetSignalSource("Automator.State", behavior)().AsRawNumber);
  }

  public static void BuildsStateSignalDefinition() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Automator { IsTransmitter = true });

    var signalDef = component.GetSignalDefinition("Automator.State", behavior);

    Assert.Equal("Automator.State", signalDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.Number, signalDef.Result.ValueType);
    Assert.Equal(ValueDef.NumericFormatEnum.Boolean, signalDef.Result.DisplayNumericFormat);
    Assert.Equal(100, int.Parse(signalDef.Result.Options[0].Value));
    Assert.Equal(0, int.Parse(signalDef.Result.Options[1].Value));
  }

  public static void ExposesRemoteTransmitterSignalByStableEntityId() {
    var component = CreateComponent(out var registry, out _);
    var entityId = Guid.NewGuid();
    var automator = Transmitter(entityId, "Source", AutomatorState.On);
    registry.Register(automator);

    var signalName = RemoteSignalName(component);
    registry.Unregister(automator);
    registry.Register(Transmitter(entityId, "Renamed source", AutomatorState.Off));

    Assert.True(signalName.StartsWith("Automator.RemoteState.", StringComparison.Ordinal));
    Assert.Equal(signalName, RemoteSignalName(component));
  }

  public static void HidesRemoteSignalWhenSourceIsTheEditedBuilding() {
    var component = CreateComponent(out var registry, out _);
    var entityId = Guid.NewGuid();
    var automator = Transmitter(entityId, "Source", AutomatorState.On);
    registry.Register(automator);
    var behavior = new AutomationBehavior();
    behavior.SetComponent(automator);
    behavior.SetComponent(automator.GetComponent<EntityComponent>());

    var signalNames = component.GetSignalNamesForBuilding(behavior);

    Assert.Equal(1, signalNames.Length);
    Assert.Equal("Automator.State", signalNames[0]);
  }

  public static void ReadsRemoteTransmitterState() {
    var component = CreateComponent(out var registry, out _);
    var entityId = Guid.NewGuid();
    var automator = Transmitter(entityId, "Source", AutomatorState.On);
    registry.Register(automator);
    var signalName = RemoteSignalName(component);
    var source = component.GetSignalSource(signalName, new AutomationBehavior());

    Assert.Equal(100, source().AsRawNumber);

    automator.State = AutomatorState.Off;

    Assert.Equal(0, source().AsRawNumber);
  }

  public static void BuildsRemoteBooleanSignalDefinition() {
    var component = CreateComponent(out var registry, out _);
    var entityId = Guid.NewGuid();
    var automator = Transmitter(entityId, "Source", AutomatorState.On);
    registry.Register(automator);
    var signalName = RemoteSignalName(component);

    var signalDef = component.GetSignalDefinition(signalName, new AutomationBehavior());

    Assert.Equal(signalName, signalDef.ScriptName);
    Assert.Equal("Source", signalDef.DisplayNameArgument);
    Assert.Equal("IgorZ.Automation.Scriptable.Automator.Signal.RemoteState:Source", signalDef.DisplayName);
    Assert.Equal(SignalDef.ScopeEnum.Global, signalDef.Scope);
    Assert.Equal(ValueDef.NumericFormatEnum.Boolean, signalDef.Result.DisplayNumericFormat);

    automator.AutomatorName = "Renamed source";

    Assert.Equal("IgorZ.Automation.Scriptable.Automator.Signal.RemoteState:Renamed source", signalDef.DisplayName);
  }

  public static void RejectsRemoteSignalDefinitionAfterSourceDeletion() {
    var component = CreateComponent(out var registry, out _);
    var automator = Transmitter(Guid.NewGuid(), "Source", AutomatorState.On);
    registry.Register(automator);
    var signalName = RemoteSignalName(component);
    registry.Unregister(automator);

    var error = Assert.Throws<ScriptError.LocParsingError>(
        () => component.GetSignalDefinition(signalName, new AutomationBehavior()));

    Assert.Equal("IgorZ.Automation.Scripting.Error.AutomatorSignalUnavailable", error.LocKey);
  }

  public static void ReportsRuntimeErrorWhenCompiledRemoteSourceIsDeleted() {
    var component = CreateComponent(out var registry, out _);
    var entityId = Guid.NewGuid();
    var automator = Transmitter(entityId, "Source", AutomatorState.On);
    registry.Register(automator);
    var source = component.GetSignalSource(RemoteSignalName(component), new AutomationBehavior());
    registry.Unregister(automator);

    var error = Assert.Throws<ScriptError.RuntimeError>(() => source());

    Assert.Equal("IgorZ.Automation.Scripting.Error.AutomatorSignalUnavailable", error.LocKey);
  }

  public static void ReportsUnknownSignal() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Automator { IsTransmitter = true });

    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalDefinition("Automator.Missing", behavior));
  }

  static AutomatorScriptableComponent CreateComponent() {
    return CreateComponent(out _, out _);
  }

  static AutomatorScriptableComponent CreateComponent(
      out AutomatorRegistry registry, out AutomatorSignalReferenceManager signalReferenceManager) {
    registry = new AutomatorRegistry();
    var managerConstructor = typeof(AutomatorSignalReferenceManager).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(AutomatorRegistry), typeof(EventBus)],
        null);
    signalReferenceManager = (AutomatorSignalReferenceManager)managerConstructor.Invoke([registry, new EventBus()]);
    var componentConstructor = typeof(AutomatorScriptableComponent).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(AutomatorRegistry), typeof(AutomatorSignalReferenceManager)],
        null);
    var component = (AutomatorScriptableComponent)componentConstructor.Invoke([registry, signalReferenceManager]);
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  static string RemoteSignalName(AutomatorScriptableComponent component) {
    return component.GetSignalNamesForBuilding(new AutomationBehavior()).Single();
  }

  static Automator Transmitter(Guid entityId, string name, AutomatorState state) {
    var automator = new Automator {
        IsTransmitter = true,
        AutomatorName = name,
        State = state,
    };
    automator.SetComponent(new EntityComponent { EntityId = entityId });
    return automator;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return args.Length == 0 ? key : key + ":" + string.Join(",", args);
    }
  }
}
