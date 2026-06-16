using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

namespace Automation.Tests;

static class ReferenceManagerTests {
  public static void TracksActionRegistrations() {
    var manager = CreateManager(out _, out _, out var action);

    manager.AddAction(action);

    Assert.Equal(1, manager.Actions.Count);
    Assert.Throws<InvalidOperationException>(() => manager.AddAction(action));

    manager.RemoveAction(action);

    Assert.Equal(0, manager.Actions.Count);
    Assert.Throws<InvalidOperationException>(() => manager.RemoveAction(action));
  }

  public static void TracksSignalRegistrations() {
    var manager = CreateManager(out _, out var signal, out _);
    var listener = new TestSignalListener();

    manager.AddSignal(signal, listener);

    Assert.Equal(1, manager.Signals.Count);
    Assert.Equal(1, manager.Signals[listener].Count);
    Assert.Throws<InvalidOperationException>(() => manager.AddSignal(signal, listener));

    manager.RemoveSignal(signal, listener);

    Assert.Equal(0, manager.Signals.Count);
    Assert.Throws<InvalidOperationException>(() => manager.RemoveSignal(signal, listener));
  }

  public static void NotifiesHostOnceForMatchingSignal() {
    var manager = CreateManager(out var service, out var signal, out _);
    var secondSignal = CreateSignal(service);
    var listener = new TestSignalListener();

    manager.AddSignal(signal, listener);
    manager.AddSignal(secondSignal, listener);
    manager.TriggerSignalUpdate("Signals.Custom");

    Assert.Equal(1, listener.Calls);
    Assert.Equal("Signals.Custom", listener.LastSignalName);
  }

  public static void AllowsRegistrationChangesWhileNotifying() {
    var manager = CreateManager(out var service, out var signal, out _);
    TestSignalListener listener = null;
    listener = new TestSignalListener {
        OnValueChangedAction = _ => manager.RemoveSignal(signal, listener),
    };

    manager.AddSignal(signal, listener);
    manager.TriggerSignalUpdate("Signals.Custom");

    Assert.Equal(1, listener.Calls);
    Assert.Equal(0, manager.Signals.Count);

    var secondListener = new TestSignalListener();
    manager.AddSignal(CreateSignal(service), secondListener);
    manager.TriggerSignalUpdate("Signals.Custom");

    Assert.Equal(1, secondListener.Calls);
  }

  static ReferenceManager CreateManager(
      out ScriptingService service, out SignalOperator signal, out ActionOperator action) {
    var scriptable = new TestScriptable("Signals");
    scriptable.RegisterSignal("Signals.Custom", ScriptValue.TypeEnum.Number);
    scriptable.RegisterAction("Signals.Do");
    service = TestScripting.CreateService(scriptable);
    signal = CreateSignal(service);
    action = ActionOperator.Create(new ExpressionContext { ScriptHost = new AutomationBehavior() }, "Signals.Do", []);
    return new ReferenceManager(service);
  }

  static SignalOperator CreateSignal(ScriptingService service) {
    return SignalOperator.Create(new ExpressionContext { ScriptHost = new AutomationBehavior() }, "Signals.Custom");
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
