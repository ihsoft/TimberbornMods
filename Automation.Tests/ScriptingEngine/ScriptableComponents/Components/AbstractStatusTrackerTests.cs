using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

namespace Automation.Tests;

static class AbstractStatusTrackerTests {
  public static void TracksActionsAndReportsDuplicates() {
    var tracker = new TestStatusTracker();
    var action = CreateAction();

    Assert.False(tracker.HasActions);
    Assert.True(tracker.AddAction(action));
    Assert.True(tracker.HasActions);
    Assert.Throws<InvalidOperationException>(() => tracker.AddAction(action));
    Assert.False(tracker.RemoveAction(action));
    Assert.False(tracker.HasActions);
    Assert.Throws<InvalidOperationException>(() => tracker.RemoveAction(action));
  }

  public static void TracksSignalsAndReportsDuplicates() {
    var tracker = new TestStatusTracker();
    var signal = CreateSignal(() => ScriptValue.FromInt(1));
    var listener = new TestSignalListener();

    Assert.True(tracker.AddSignal(signal, listener));
    var secondSignal = CreateSignal(() => ScriptValue.FromInt(1));
    Assert.False(tracker.AddSignal(secondSignal, listener));
    Assert.Throws<InvalidOperationException>(() => tracker.AddSignal(signal, listener));
    Assert.True(tracker.RemoveSignal(signal, listener));
    Assert.False(tracker.RemoveSignal(secondSignal, listener));
  }

  public static void NotifiesListenersOnlyWhenValueChanges() {
    TestScripting.CreateService();
    var tracker = new TestStatusTracker();
    var value = 1;
    var firstListener = new TestSignalListener();
    var secondListener = new TestSignalListener();
    var firstSignal = CreateSignal(() => ScriptValue.FromInt(value));

    tracker.AddSignal(firstSignal, firstListener);
    tracker.AddSignal(CreateSignal(() => ScriptValue.FromInt(value)), secondListener);

    tracker.TriggerSignalUpdate("Signals.Value");

    Assert.Equal(0, firstListener.Calls);
    Assert.Equal(0, secondListener.Calls);

    value = 2;
    tracker.TriggerSignalUpdate("Signals.Value");

    Assert.Equal(1, firstListener.Calls);
    Assert.Equal(1, secondListener.Calls);
    Assert.Equal("Signals.Value", firstListener.LastSignalName);
  }

  static SignalOperator CreateSignal(Func<ScriptValue> source) {
    var signals = new TestScriptable("Signals");
    signals.RegisterSignal("Signals.Value", ScriptValue.TypeEnum.Number, source);
    TestScripting.CreateService(signals);
    return SignalOperator.Create(new ExpressionContext { ScriptHost = new AutomationBehavior() }, "Signals.Value");
  }

  static ActionOperator CreateAction() {
    var actions = new TestScriptable("Actions");
    actions.RegisterAction("Actions.Do");
    TestScripting.CreateService(actions);
    return ActionOperator.Create(new ExpressionContext { ScriptHost = new AutomationBehavior() }, "Actions.Do", []);
  }

  sealed class TestStatusTracker : AbstractStatusTracker {
  }

  sealed class TestSignalListener : ISignalListener {
    public AutomationBehavior Behavior { get; } = new();
    public int Calls { get; private set; }
    public string LastSignalName { get; private set; }

    public void OnValueChanged(string signalName) {
      Calls++;
      LastSignalName = signalName;
    }
  }
}
