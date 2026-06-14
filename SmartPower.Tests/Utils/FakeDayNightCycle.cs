using Timberborn.TimeSystem;

namespace SmartPower.Tests;

sealed class FakeDayNightCycle : IDayNightCycle {
  public float FixedDeltaTimeInHours { get; set; }
}
