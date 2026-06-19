using System;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.GameCycleSystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.TimeSystem;
using Timberborn.WorkSystem;

namespace Automation.Tests;

static class TimeScriptableComponentTests {
  public static void ExposesTimeSignalsAndDefinitions() {
    var harness = new Harness();

    var signalNames = harness.Component.GetSignalNamesForBuilding(harness.Behavior);
    var minuteDef = harness.Component.GetSignalDefinition("Time.MinuteOfDay", harness.Behavior);
    var dayDef = harness.Component.GetSignalDefinition("Time.Day", harness.Behavior);
    var workingHoursDef = harness.Component.GetSignalDefinition("Time.WorkingHours", harness.Behavior);

    Assert.Equal("Time", harness.Component.Name);
    Assert.Equal("Time.MinuteOfDay", signalNames[0]);
    Assert.Equal("Time.Day", signalNames[1]);
    Assert.Equal("Time.WorkingHours", signalNames[2]);
    Assert.Equal(ValueDef.NumericFormatEnum.Integer, minuteDef.Result.DisplayNumericFormat);
    Assert.Equal((0, 1430), minuteDef.Result.DisplayNumericFormatRange);
    Assert.Equal(ValueDef.NumericFormatEnum.Integer, dayDef.Result.DisplayNumericFormat);
    Assert.Equal(ScriptValue.TypeEnum.String, workingHoursDef.Result.ValueType);
    Assert.Equal("Working", workingHoursDef.Result.Options[0].Value);
    Assert.Equal("OffHours", workingHoursDef.Result.Options[1].Value);
  }

  public static void ReadsTimeSignals() {
    var harness = new Harness();
    harness.DayNightCycle.DayNumber = 12;
    harness.DayNightCycle.HoursPassedToday = 8.63f;

    Assert.Equal(51000, harness.Component.GetSignalSource("Time.MinuteOfDay", harness.Behavior)().AsRawNumber);
    Assert.Equal(1200, harness.Component.GetSignalSource("Time.Day", harness.Behavior)().AsRawNumber);
    Assert.Equal("OffHours", harness.Component.GetSignalSource("Time.WorkingHours", harness.Behavior)().AsString);

    harness.WorkingHoursManager.AreWorkingHours = true;

    Assert.Equal("Working", harness.Component.GetSignalSource("Time.WorkingHours", harness.Behavior)().AsString);
  }

  public static void SchedulesMinuteOfDayUpdatesOnlyWithListeners() {
    var harness = new Harness();
    var listener = new TestSignalListener(harness.Behavior);

    harness.Component.PostLoad();

    Assert.Equal(0, harness.TimeTriggerFactory.CreatedTriggers);

    harness.Component.RegisterSignalChangeCallback(Signal("Time.Day", harness.Behavior), listener);

    Assert.Equal(0, harness.TimeTriggerFactory.CreatedTriggers);

    harness.DayNightCycle.HoursPassedToday = 8.95f;
    var minuteOfDaySignal = Signal("Time.MinuteOfDay", harness.Behavior);
    harness.Component.RegisterSignalChangeCallback(minuteOfDaySignal, listener);

    Assert.Equal(1, harness.TimeTriggerFactory.CreatedTriggers);
    Assert.True(harness.TimeTriggerFactory.LastTrigger.InProgress);
    Assert.True(Math.Abs(0.0021f - harness.TimeTriggerFactory.LastDelayInDays) < 0.0001f);

    harness.TimeTriggerFactory.LastTrigger.Fire();

    Assert.Equal(1, listener.Calls);
    Assert.Equal("Time.MinuteOfDay", listener.LastSignalName);
    Assert.Equal(2, harness.TimeTriggerFactory.CreatedTriggers);

    var lastTrigger = harness.TimeTriggerFactory.LastTrigger;
    harness.Component.UnregisterSignalChangeCallback(minuteOfDaySignal, listener);

    Assert.False(lastTrigger.InProgress);
  }

  public static void EventsNotifyMatchingListeners() {
    var harness = new Harness();
    var listener = new TestSignalListener(harness.Behavior);
    harness.Component.RegisterSignalChangeCallback(Signal("Time.Day", harness.Behavior), listener);
    harness.Component.RegisterSignalChangeCallback(Signal("Time.WorkingHours", harness.Behavior), listener);

    harness.Component.OnCycleDayStarted(new CycleDayStartedEvent());
    harness.Component.OnWorkingHoursChanged(new WorkingHoursChangedEvent());
    harness.Component.OnWorkingHoursTransitioned(new WorkingHoursTransitionedEvent());

    Assert.Equal(3, listener.Calls);
    Assert.Equal("Time.WorkingHours", listener.LastSignalName);
  }

  static SignalOperator Signal(string signalName, AutomationBehavior behavior) {
    return SignalOperator.Create(new ExpressionContext { ScriptHost = behavior }, signalName);
  }

  sealed class Harness {
    public readonly AutomationBehavior Behavior = new();
    public readonly TestDayNightCycle DayNightCycle = new();
    public readonly TestTimeTriggerFactory TimeTriggerFactory = new();
    public readonly WorkingHoursManager WorkingHoursManager = new();
    public readonly TimeScriptableComponent Component;

    public Harness() {
      var service = TestScripting.CreateService();
      Component = CreateComponent(
          new EventBus(), DayNightCycle, TimeTriggerFactory, WorkingHoursManager, new ReferenceManager(service));
      Component.InjectDependencies(new TestLoc(), service);
      Component.Load();
    }

    static TimeScriptableComponent CreateComponent(
        EventBus eventBus, IDayNightCycle dayNightCycle, ITimeTriggerFactory timeTriggerFactory,
        WorkingHoursManager workingHoursManager, ReferenceManager referenceManager) {
      var constructor = typeof(TimeScriptableComponent).GetConstructor(
          BindingFlags.Instance | BindingFlags.NonPublic,
          null,
          [
              typeof(EventBus),
              typeof(IDayNightCycle),
              typeof(ITimeTriggerFactory),
              typeof(WorkingHoursManager),
              typeof(ReferenceManager),
          ],
          null);
      return (TimeScriptableComponent)constructor.Invoke(
          [eventBus, dayNightCycle, timeTriggerFactory, workingHoursManager, referenceManager]);
    }
  }

  sealed class TestDayNightCycle : IDayNightCycle {
    public int DayNumber { get; set; } = 1;
    public float HoursPassedToday { get; set; }
  }

  sealed class TestTimeTriggerFactory : ITimeTriggerFactory {
    public int CreatedTriggers { get; private set; }
    public float LastDelayInDays { get; private set; }
    public TestTimeTrigger LastTrigger { get; private set; }

    public ITimeTrigger Create(Action action, float delayInDays) {
      CreatedTriggers++;
      LastDelayInDays = delayInDays;
      LastTrigger = new TestTimeTrigger(action);
      return LastTrigger;
    }
  }

  sealed class TestTimeTrigger(Action action) : ITimeTrigger {
    public bool InProgress { get; private set; }

    public void Resume() {
      InProgress = true;
    }

    public void Pause() {
      InProgress = false;
    }

    public void Fire() {
      if (!InProgress) {
        return;
      }
      InProgress = false;
      action();
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

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
