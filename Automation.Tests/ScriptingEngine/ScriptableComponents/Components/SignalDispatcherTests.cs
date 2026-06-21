using System;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.BaseComponentSystem;
using Timberborn.EntitySystem;
using Timberborn.SingletonSystem;

namespace Automation.Tests;

static class SignalDispatcherTests {
  public static void AggregatesProviderValues() {
    var dispatcher = CreateDispatcher();
    var firstProvider = Provider("first");
    var secondProvider = Provider("second");
    var firstAction = new object();
    var secondAction = new object();

    dispatcher.RegisterSignalProvider("Signals.Custom", firstProvider, firstAction);
    dispatcher.RegisterSignalProvider("Signals.Custom", secondProvider, secondAction);
    dispatcher.SetSignalValue("Signals.Custom", 100, firstProvider);
    dispatcher.SetSignalValue("Signals.Custom", 300, secondProvider);

    Assert.Equal(300, dispatcher.GetSignalValue("Signals.Custom"));
    Assert.Equal(2, dispatcher.GetSignalValue("Signals.Custom.Count"));
    Assert.Equal(100, dispatcher.GetSignalValue("Signals.Custom.Min"));
    Assert.Equal(300, dispatcher.GetSignalValue("Signals.Custom.Max"));
    Assert.Equal(400, dispatcher.GetSignalValue("Signals.Custom.Sum"));
    Assert.Equal(200, dispatcher.GetSignalValue("Signals.Custom.Avg"));

    dispatcher.UnregisterSignalProvider("Signals.Custom", firstProvider, firstAction);

    Assert.Equal(1, dispatcher.GetSignalValue("Signals.Custom.Count"));
    Assert.Equal(300, dispatcher.GetSignalValue("Signals.Custom.Min"));
  }

  public static void HandlesManualSignalValues() {
    var dispatcher = CreateDispatcher();

    dispatcher.SetManualSignalValue("Signals.Manual", 42);

    Assert.True(dispatcher.HasManualSignalValue("Signals.Manual"));
    Assert.Equal("Signals.Manual", dispatcher.GetRegisteredSignals()[0]);
    Assert.Equal(42, dispatcher.GetSignalValue("Signals.Manual"));
    Assert.Equal(1, dispatcher.GetSignalValue("Signals.Manual.Count"));

    dispatcher.UnsetManualSignalValue("Signals.Manual");

    Assert.False(dispatcher.HasManualSignalValue("Signals.Manual"));
    Assert.Equal(0, dispatcher.GetSignalValue("Signals.Manual"));
  }

  public static void NotifiesListenersOnSignalChanges() {
    var dispatcher = CreateDispatcher(out var service);
    var listener = new TestSignalListener();
    var signal = Signal("Signals.Custom");

    dispatcher.RegisterSignalListener(signal, listener);
    dispatcher.SetManualSignalValue("Signals.Custom", 1);
    dispatcher.SetManualSignalValue("Signals.Custom", 1);
    dispatcher.SetManualSignalValue("Signals.Custom", 2);

    Assert.Equal(2, listener.Calls);
    Assert.Equal("Signals.Custom", listener.LastSignalName);
    Assert.Equal(0, service.GetExecutionStackSize());
  }

  public static void SeparatesSignalListAndValueEvents() {
    var dispatcher = CreateDispatcher();
    var signalsChangedCalls = 0;
    var signalValuesChangedCalls = 0;
    dispatcher.SignalsChanged += (_, _) => signalsChangedCalls++;
    dispatcher.SignalValuesChanged += (_, _) => signalValuesChangedCalls++;

    dispatcher.SetManualSignalValue("Signals.Custom", 1);
    dispatcher.SetManualSignalValue("Signals.Custom", 1);
    dispatcher.SetManualSignalValue("Signals.Custom", 2);

    Assert.Equal(1, signalsChangedCalls);
    Assert.Equal(2, signalValuesChangedCalls);

    var provider = Provider("provider");
    var action = new object();
    dispatcher.RegisterSignalProvider("Signals.Custom", provider, action);
    dispatcher.SetSignalValue("Signals.Custom", 3, provider);

    Assert.Equal(2, signalsChangedCalls);
    Assert.Equal(3, signalValuesChangedCalls);
  }

  public static void RejectsDuplicateAndMissingRegistrations() {
    var dispatcher = CreateDispatcher();
    var listener = new TestSignalListener();
    var signal = Signal("Signals.Custom");
    var provider = Provider("provider");
    var action = new object();

    dispatcher.RegisterSignalListener(signal, listener);
    Assert.Throws<InvalidOperationException>(() => dispatcher.RegisterSignalListener(signal, listener));
    dispatcher.UnregisterSignalListener(signal, listener);
    Assert.Throws<InvalidOperationException>(() => dispatcher.UnregisterSignalListener(signal, listener));

    dispatcher.RegisterSignalProvider("Signals.Custom", provider, action);
    Assert.Throws<InvalidOperationException>(
        () => dispatcher.RegisterSignalProvider("Signals.Custom", provider, action));
    dispatcher.UnregisterSignalProvider("Signals.Custom", provider, action);
    Assert.Throws<InvalidOperationException>(
        () => dispatcher.UnregisterSignalProvider("Signals.Custom", provider, action));
    Assert.Throws<InvalidOperationException>(() => dispatcher.UnsetManualSignalValue("Signals.Custom"));
  }

  public static void LocksChangesWhileNotifying() {
    var dispatcher = CreateDispatcher();
    var listener = new TestSignalListener {
        OnValueChangedAction =
            _ => dispatcher.RegisterSignalListener(Signal("Signals.Other"), new TestSignalListener()),
    };

    dispatcher.RegisterSignalListener(Signal("Signals.Custom"), listener);

    Assert.Throws<InvalidOperationException>(() => dispatcher.SetManualSignalValue("Signals.Custom", 1));
    Assert.Equal(1, listener.Calls);
  }

  static SignalDispatcher CreateDispatcher() {
    return CreateDispatcher(out _);
  }

  static SignalDispatcher CreateDispatcher(out ScriptingService service) {
    service = TestScripting.CreateService();
    var constructor = typeof(SignalDispatcher).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(ScriptingService), typeof(EventBus)],
        null);
    return (SignalDispatcher)constructor.Invoke([service, new EventBus()]);
  }

  static SignalOperator Signal(string expression) {
    var signals = new TestScriptable("Signals");
    signals.RegisterSignal(expression, ScriptValue.TypeEnum.Number);
    TestScripting.CreateService(signals);
    return SignalOperator.Create(new ExpressionContext { ScriptHost = new AutomationBehavior() }, expression);
  }

  static BaseComponent Provider(string entityId) {
    var provider = new BaseComponent();
    provider.SetComponent(new EntityComponent { EntityId = entityId });
    return provider;
  }

  sealed class TestSignalListener : ISignalListener {
    public AutomationBehavior Behavior { get; } = new();
    public int Calls { get; private set; }
    public string LastSignalName { get; private set; }
    public Action<string> OnValueChangedAction { get; init; }

    public void OnValueChanged(string signalName) {
      Calls++;
      LastSignalName = signalName;
      OnValueChangedAction?.Invoke(signalName);
    }
  }
}
