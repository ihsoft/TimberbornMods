using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;

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

  public static void UsesProvidedValueWithoutReevaluatingSource() {
    var tracker = new TestStatusTracker();
    var sourceCalls = 0;
    var signal = CreateSignal(() => {
      sourceCalls++;
      return ScriptValue.FromInt(1);
    });
    var listener = new TestSignalListener();

    tracker.AddSignal(signal, listener);
    Assert.Equal(1, sourceCalls);

    tracker.TriggerSignalUpdate("Signals.Value", ScriptValue.FromInt(1));
    Assert.Equal(1, sourceCalls);
    Assert.Equal(0, listener.Calls);

    tracker.TriggerSignalUpdate("Signals.Value", ScriptValue.FromInt(2));
    Assert.Equal(1, sourceCalls);
    Assert.Equal(1, listener.Calls);
    Assert.Equal("Signals.Value", listener.LastSignalName);
  }

  public static void ComparesProvidedValueWithRestoredLastValue() {
    var componentKey = new ComponentKey(typeof(TestStatusTracker).FullName);
    var savedTracker = new TestStatusTracker();
    var savedListener = new TestSignalListener();
    savedTracker.AddSignal(CreateSignal(() => ScriptValue.FromInt(1)), savedListener);
    var saver = new TestEntitySaver();
    savedTracker.Save(saver);

    var loader = new TestEntityLoader();
    loader.SetComponent(componentKey, new IObjectLoader(saver.Components[componentKey.Name].Values));
    var restoredTracker = new TestStatusTracker();
    restoredTracker.Load(loader);
    var sourceCalls = 0;
    var restoredSignal = CreateSignal(() => {
      sourceCalls++;
      return ScriptValue.FromInt(2);
    });
    var restoredListener = new TestSignalListener();
    restoredTracker.AddSignal(restoredSignal, restoredListener);

    restoredTracker.TriggerSignalUpdate("Signals.Value", ScriptValue.FromInt(2));

    Assert.Equal(0, sourceCalls);
    Assert.Equal(1, restoredListener.Calls);
    Assert.Equal("Signals.Value", restoredListener.LastSignalName);
  }

  public static void NotifiesSameListenerOnceForMultipleSignalRegistrations() {
    TestScripting.CreateService();
    var tracker = new TestStatusTracker();
    var value = 1;
    var listener = new TestSignalListener();

    tracker.AddSignal(CreateSignal(() => ScriptValue.FromInt(value)), listener);
    tracker.AddSignal(CreateSignal(() => ScriptValue.FromInt(value)), listener);

    value = 2;
    tracker.TriggerSignalUpdate("Signals.Value");

    Assert.Equal(1, listener.Calls);
    Assert.Equal("Signals.Value", listener.LastSignalName);
  }

  public static void LoadIgnoresComponentWithoutSavedSignals() {
    var tracker = new TestStatusTracker();
    var loader = new TestEntityLoader();
    loader.SetComponent(
        new ComponentKey(typeof(TestStatusTracker).FullName),
        new IObjectLoader(new Dictionary<string, object>()));

    tracker.Load(loader);
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
