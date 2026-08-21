using System;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Automation;
using Timberborn.EntitySystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;

namespace Automation.Tests;

static class AutomatorSignalReferenceManagerTests {
  public static void NotifiesListenerOnceForMultipleReferencesToSameSource() {
    var registry = new AutomatorRegistry();
    var manager = CreateManager(registry, new EventBus());
    var component = CreateComponent(registry, manager);
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    component.Load();
    var source = Transmitter(Guid.NewGuid());
    registry.Register(source);
    var signalName = component.GetSignalNamesForBuilding(new AutomationBehavior())[0];
    var listener = new TestSignalListener();
    var context = new ExpressionContext { ScriptHost = listener.Behavior };
    var firstReference = SignalOperator.Create(context, signalName);
    var secondReference = SignalOperator.Create(context, signalName);
    component.RegisterSignalChangeCallback(firstReference, listener);
    component.RegisterSignalChangeCallback(secondReference, listener);

    manager.OnStateChanged(source);

    Assert.Equal(1, listener.Calls);

    component.UnregisterSignalChangeCallback(firstReference, listener);
    manager.OnStateChanged(source);

    Assert.Equal(2, listener.Calls);

    component.UnregisterSignalChangeCallback(secondReference, listener);
    manager.OnStateChanged(source);

    Assert.Equal(2, listener.Calls);
  }

  public static void LoadAndUnloadManageEventRegistrationAndStaticBridge() {
    var eventBus = new EventBus();
    var manager = CreateManager(new AutomatorRegistry(), eventBus);

    manager.Load();

    Assert.True(eventBus.RegisteredObjects.Contains(manager));
    Assert.Same(manager, AutomatorSignalReferenceManager.Instance);

    manager.Unload();

    Assert.False(eventBus.RegisteredObjects.Contains(manager));
    Assert.Equal(null, AutomatorSignalReferenceManager.Instance);
  }

  static AutomatorScriptableComponent CreateComponent(
      AutomatorRegistry registry, AutomatorSignalReferenceManager manager) {
    var constructor = typeof(AutomatorScriptableComponent).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(AutomatorRegistry), typeof(AutomatorSignalReferenceManager)],
        null);
    return (AutomatorScriptableComponent)constructor.Invoke([registry, manager]);
  }

  static AutomatorSignalReferenceManager CreateManager(AutomatorRegistry registry, EventBus eventBus) {
    var constructor = typeof(AutomatorSignalReferenceManager).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(AutomatorRegistry), typeof(EventBus)],
        null);
    return (AutomatorSignalReferenceManager)constructor.Invoke([registry, eventBus]);
  }

  static Automator Transmitter(Guid entityId) {
    var automator = new Automator {
        IsTransmitter = true,
        AutomatorName = "Source",
        State = AutomatorState.On,
    };
    automator.SetComponent(new EntityComponent { EntityId = entityId });
    return automator;
  }

  sealed class TestSignalListener : ISignalListener {
    public AutomationBehavior Behavior { get; } = new();
    public int Calls { get; private set; }

    public void OnValueChanged(string signalName) {
      Calls++;
    }
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
