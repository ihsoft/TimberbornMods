// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using Timberborn.GameCycleSystem;
using Timberborn.SingletonSystem;
using Timberborn.TimeSystem;
using Timberborn.WorkSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class TimeScriptableComponent : ScriptableComponentBase, IPostLoadableSingleton {

  const string MinuteOfDaySignalLocKey = "IgorZ.Automation.Scriptable.Time.Signal.MinuteOfDay";
  const string DaySignalLocKey = "IgorZ.Automation.Scriptable.Time.Signal.Day";
  const string WorkingHoursSignalLocKey = "IgorZ.Automation.Scriptable.Time.Signal.WorkingHours";
  const string WorkingHoursWorkingLocKey = "IgorZ.Automation.Scriptable.Time.Signal.WorkingHours.Working";
  const string WorkingHoursOffHoursLocKey = "IgorZ.Automation.Scriptable.Time.Signal.WorkingHours.OffHours";

  const string MinuteOfDaySignalName = "Time.MinuteOfDay";
  const string DaySignalName = "Time.Day";
  const string WorkingHoursSignalName = "Time.WorkingHours";
  const string WorkingHoursWorkingValue = "Working";
  const string WorkingHoursOffHoursValue = "OffHours";

  const int MinutesPerDay = 24 * 60;
  const int MinuteOfDaySignalStep = 10;

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Time";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return [MinuteOfDaySignalName, DaySignalName, WorkingHoursSignalName];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    return name switch {
        MinuteOfDaySignalName => MinuteOfDaySignal,
        DaySignalName => DaySignal,
        WorkingHoursSignalName => WorkingHoursSignal,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    return name switch {
        MinuteOfDaySignalName => MinuteOfDaySignalDef,
        DaySignalName => DaySignalDef,
        WorkingHoursSignalName => WorkingHoursSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName is not MinuteOfDaySignalName and not DaySignalName and not WorkingHoursSignalName) {
      throw new InvalidOperationException($"Unknown signal: {signalOperator.SignalName}");
    }
    var wasMinuteOfDayInactive = !HasMinuteOfDayListeners;
    _referenceManager.AddSignal(signalOperator, host);
    if (signalOperator.SignalName == MinuteOfDaySignalName && wasMinuteOfDayInactive) {
      ScheduleMinuteOfDayUpdate();
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    _referenceManager.RemoveSignal(signalOperator, host);
    if (signalOperator.SignalName == MinuteOfDaySignalName && !HasMinuteOfDayListeners) {
      StopMinuteOfDayUpdate();
    }
  }

  #endregion

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    _loaded = true;
    _eventBus.Register(this);
    ScheduleMinuteOfDayUpdate();
  }

  #endregion

  #region Signals

  SignalDef MinuteOfDaySignalDef => _minuteOfDaySignalDef ??= new SignalDef {
      ScriptName = MinuteOfDaySignalName,
      DisplayName = Loc.T(MinuteOfDaySignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
          DisplayNumericFormatRange = (0, MinutesPerDay - MinuteOfDaySignalStep),
      },
  };
  SignalDef _minuteOfDaySignalDef;

  SignalDef DaySignalDef => _daySignalDef ??= new SignalDef {
      ScriptName = DaySignalName,
      DisplayName = Loc.T(DaySignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
          DisplayNumericFormatRange = (1, float.NaN),
      },
  };
  SignalDef _daySignalDef;

  SignalDef WorkingHoursSignalDef => _workingHoursSignalDef ??= new SignalDef {
      ScriptName = WorkingHoursSignalName,
      DisplayName = Loc.T(WorkingHoursSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.String,
          Options = [
              (WorkingHoursWorkingValue, Loc.T(WorkingHoursWorkingLocKey)),
              (WorkingHoursOffHoursValue, Loc.T(WorkingHoursOffHoursLocKey)),
          ],
      },
  };
  SignalDef _workingHoursSignalDef;

  ScriptValue MinuteOfDaySignal() {
    return ScriptValue.FromInt(GetMinuteOfDayBucket());
  }

  ScriptValue DaySignal() {
    return ScriptValue.FromInt(_dayNightCycle.DayNumber);
  }

  ScriptValue WorkingHoursSignal() {
    return ScriptValue.FromString(_workingHoursManager.AreWorkingHours ? WorkingHoursWorkingValue : WorkingHoursOffHoursValue);
  }

  #endregion

  #region Implementation

  readonly EventBus _eventBus;
  readonly IDayNightCycle _dayNightCycle;
  readonly ITimeTriggerFactory _timeTriggerFactory;
  readonly WorkingHoursManager _workingHoursManager;
  readonly ReferenceManager _referenceManager;

  ITimeTrigger _minuteOfDayUpdate;
  int _minuteOfDayUpdateVersion;
  bool _loaded;

  TimeScriptableComponent(
      EventBus eventBus, IDayNightCycle dayNightCycle, ITimeTriggerFactory timeTriggerFactory,
      WorkingHoursManager workingHoursManager, ReferenceManager referenceManager) {
    _eventBus = eventBus;
    _dayNightCycle = dayNightCycle;
    _timeTriggerFactory = timeTriggerFactory;
    _workingHoursManager = workingHoursManager;
    _referenceManager = referenceManager;
  }

  bool HasMinuteOfDayListeners =>
      _referenceManager.Signals.Values.Any(signals => signals.Any(signal => signal.SignalName == MinuteOfDaySignalName));

  int GetMinuteOfDayBucket() {
    var currentMinute = Math.Floor(_dayNightCycle.HoursPassedToday * 60f);
    var minuteOfDay = (int)(Math.Floor(currentMinute / MinuteOfDaySignalStep) * MinuteOfDaySignalStep);
    return Math.Min(minuteOfDay, MinutesPerDay - MinuteOfDaySignalStep);
  }

  float GetMinutesToNextBucket() {
    var currentMinute = _dayNightCycle.HoursPassedToday * 60f;
    var nextMinute = (float)(Math.Floor(currentMinute / MinuteOfDaySignalStep) + 1) * MinuteOfDaySignalStep;
    if (nextMinute > MinutesPerDay) {
      nextMinute = MinutesPerDay;
    }
    return Math.Max(nextMinute - currentMinute, 0.01f);
  }

  void ScheduleMinuteOfDayUpdate() {
    StopMinuteOfDayUpdate();
    if (!_loaded || !HasMinuteOfDayListeners) {
      return;
    }
    var minuteOfDayUpdateVersion = ++_minuteOfDayUpdateVersion;
    var delayInDays = GetMinutesToNextBucket() / MinutesPerDay;
    _minuteOfDayUpdate = _timeTriggerFactory.Create(() => OnMinuteOfDayUpdateDue(minuteOfDayUpdateVersion), delayInDays);
    _minuteOfDayUpdate.Resume();
  }

  void StopMinuteOfDayUpdate() {
    _minuteOfDayUpdateVersion++;
    _minuteOfDayUpdate?.Pause();
    _minuteOfDayUpdate = null;
  }

  void OnMinuteOfDayUpdateDue(int minuteOfDayUpdateVersion) {
    if (minuteOfDayUpdateVersion != _minuteOfDayUpdateVersion) {
      return;
    }
    _minuteOfDayUpdate = null;
    _referenceManager.TriggerSignalUpdate(MinuteOfDaySignalName);
    ScheduleMinuteOfDayUpdate();
  }

  #endregion

  #region Event listeners

  [OnEvent]
  public void OnCycleDayStarted(CycleDayStartedEvent cycleDayStartedEvent) {
    _referenceManager.TriggerSignalUpdate(DaySignalName);
  }

  [OnEvent]
  public void OnWorkingHoursTransitioned(WorkingHoursTransitionedEvent workingHoursTransitionedEvent) {
    _referenceManager.TriggerSignalUpdate(WorkingHoursSignalName);
  }

  [OnEvent]
  public void OnWorkingHoursChanged(WorkingHoursChangedEvent workingHoursChangedEvent) {
    _referenceManager.TriggerSignalUpdate(WorkingHoursSignalName);
  }

  #endregion
}
