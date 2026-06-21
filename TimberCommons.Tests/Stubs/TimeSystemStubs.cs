using System;
using System.Collections.Generic;
using System.Linq;

namespace Timberborn.TimeSystem;

public enum TimeOfDay {
  Day,
  Night,
}

public interface IDayNightCycle {
  float FixedDeltaTimeInHours { get; }
}

public interface ITimeTrigger {
  void FastForwardProgress(float progress);
  void Reset();
  void Resume();
}

public interface ITimeTriggerFactory {
  ITimeTrigger Create(Action action, float delayInDays);
}

sealed class TestTimeTriggerFactory : ITimeTriggerFactory {
  public readonly List<TestTimeTrigger> CreatedTriggers = [];
  public TestTimeTrigger LastTrigger => CreatedTriggers.Last();

  public ITimeTrigger Create(Action action, float delayInDays) {
    var trigger = new TestTimeTrigger {
        Action = action,
        DelayInDays = delayInDays,
    };
    CreatedTriggers.Add(trigger);
    return trigger;
  }
}

sealed class TestTimeTrigger : ITimeTrigger {
  public Action Action { get; init; }
  public float DelayInDays { get; init; }
  public float FastForwardedProgress { get; private set; }
  public bool ResetCalled { get; private set; }
  public bool Resumed { get; private set; }

  public void FastForwardProgress(float progress) {
    FastForwardedProgress = progress;
  }

  public void Reset() {
    ResetCalled = true;
  }

  public void Resume() {
    Resumed = true;
  }
}
